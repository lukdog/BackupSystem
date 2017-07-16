using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Data.SQLite;
using System.IO;
namespace Progetto_Server
{
    class Server
    {
        private const String usersDb = "usersDb.sqlite";
        public const int maxVersion = 10;
        public const int readingTimeout = 50000;
        public const int writingTimeout = 50000;
        private volatile bool _shouldStop = false;

        private TcpListener sock = null;
        private SQLiteConnection users = null;
        private List<Thread> threadList = null;
        private List<Syncro> executingSync = null;
        private List<Restore> executingRestore = null;

        /// <summary>
        /// Costruttore per la Classe Server
        /// </summary>
        /// <param name="s">TcpListener attivato</param>
        public Server(TcpListener s)
        {
            sock = s;
        }

        /// <summary>
        /// Metodo che avvia il Server, si mette in attesa di connessione e lancia nuovi thread quando arriva una nuova connessione.
        /// Se non ci sono risorse disponibili per avviare un nuovo thread rifiuta la connessione.
        /// </summary>
        /// <returns>True se Server Avviato Correttamente</returns>
        public bool start()
        {
            /*Carico DB utenti*/
            Console.WriteLine("-> Carico DB Utenti...");
            users = initDbUsers();
            if (users == null) return false;
            Console.WriteLine("\tDB Utenti Caricato!");

            /*Server Pronto per Accettare Connessione*/
            Console.WriteLine("-> In Attesa di Connessione");
            ParameterizedThreadStart st = new ParameterizedThreadStart(syncConnection);
            threadList = new List<Thread>();
            executingSync = new List<Syncro>();
            executingRestore = new List<Restore>();
            try
            {
                /*Server in Attesa di Connessione*/
                while (!_shouldStop)
                {
                    TcpClient client = sock.AcceptTcpClient();
                    Console.WriteLine("\tNuova Connessione da: " + IPAddress.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()));
                    Thread t;
                    try
                    {
                        t = new Thread(st);
                        t.Start(client);
                        threadList.Add(t);
                    }
                    catch (OutOfMemoryException)
                    {
                        Console.WriteLine("_ERRORE: Non ci sono abbastanza risorse disponibili per la creazione di nuovi Thread");
                        client.Close();
                    }
                }
            }
            catch (Exception e)
            {
                _shouldStop = true;
                Console.WriteLine("_ERRORE: Un errore imprevisto ha causato la terminazione del Server: " + e.Message);
                return false;
            }
            finally
            {
                foreach (Thread t in threadList)
                    t.Join();

                if (sock != null)
                    sock.Stop();

            }

            return true;
        }

        /// <summary>
        /// Metodo eseguito dai Thread che si collegano con il client, si occupa di eseguire l'autenticazione dell'utente,
        /// Interpretare la richiesta ed avviare la sincronizzazione
        /// </summary>
        /// <param name="o">Oggetto di tipo TcpClient</param>
        private void syncConnection(Object o)
        {
            TcpClient client = (TcpClient)o;
            Stream stream = client.GetStream();
            stream.ReadTimeout = readingTimeout;
            stream.WriteTimeout = writingTimeout;
            int thID = Thread.CurrentThread.ManagedThreadId;

            Console.WriteLine("({0}) -> Avviato nuovo Thread", thID);

            /*Ricevo comando inviato*/
            Command c = Command.deserializeFrom(client, stream, ref _shouldStop);
            if (_shouldStop)
            {
                stream.Close();
                client.Close();
                return;
            }
            else if (c == null || c.settings == null)
            {
                Console.WriteLine("(" + thID + ") -> Comandi non Ricevuti dal Client");
                stream.Close();
                client.Close();
                return;
            }
            else
            {
                Console.WriteLine("(" + thID + ") -> Tentativo di Autenticazione da " + c.settings.user);
            }

            /*Controllo Autenticazione*/
            if (!authUser(c.settings, c.cmd))
            {
                Console.WriteLine("(" + thID + ") -> Autenticazione non Riuscita per utente " + c.settings.user);
                Command err = new Command(Command.type.ERRPSW);
                err.serializeTo(stream);
                stream.Close();
                client.Close();
                GC.Collect();
                return;
            }

            /*Autenticazione Riuscita*/
            Console.WriteLine("(" + thID + ") -> Autenticazione Riuscita per utente " + c.settings.user);

            if (c.cmd == Command.type.SYNC_REQ)
            {
                Console.WriteLine("(" + thID + ") -> Richiesta Sincronizzazione da utente " + c.settings.user);
                Syncro s = new Syncro(client, c.settings);
                executingSync.Add(s);
                s.start();
                executingSync.Remove(s);
            }
            else if (c.cmd == Command.type.LIST_REQ)
            {
                Console.WriteLine("(" + thID + ") -> Richiesto elenco file per il ripristino da utente " + c.settings.user);
                Restore r = new Restore(client, c.settings);
                executingRestore.Add(r);
                r.sendListFiles();
                executingRestore.Remove(r);
            }
            else if (c.cmd == Command.type.VERS_REQ)
            {
                Restore r = new Restore(client, c.settings);
                if (c.versions != null && c.versions.Count == 1)
                {
                    String path = c.versions[0].path;
                    Console.WriteLine("(" + thID + ") -> Richiesto elenco versioni del path: {0} per il ripristino da parte dell'utente " + c.settings.user, path);
                    executingRestore.Add(r);
                    r.sendVersions(path, c.versions[0].isDirectory);
                    executingRestore.Remove(r);
                }
            }
            else if (c.cmd == Command.type.REST_REQ)
            {
                Restore r = new Restore(client, c.settings);
                if (c.versions != null && c.versions.Count == 1)
                {
                    Console.WriteLine("(" + thID + ") -> Richiesto Ripristino del path: {0} per il ripristino da parte dell'utente " + c.settings.user, c.versions[0].path);
                    executingRestore.Add(r);
                    r.sendFiles(c.versions[0]);
                    executingRestore.Remove(r);
                }
            }

            Console.WriteLine("(" + thID + ") -> Sincronizzazione Terminata per l'utente:" + c.settings.user);

            stream.Close();
            client.Close();
            GC.Collect();
        }

        /// <summary>
        /// Metodo che controlla se l'utente esiste e se la pwd è corretta, se non esiste lo crea,
        /// si occupa anche di creare Directory e DB per l'utente
        /// </summary>
        /// <param name="s">Settings Relative alla richiesta ricevuta</param>
        /// <param name="cmdtype">Tipo di Comando con cui è stata richiesta l'autenticazione</param>
        /// <returns>True se loggato con successo</returns>
        private bool authUser(Settings s, Command.type cmdtype)
        {
            /*Controllo se esiste user nel DB*/
            Settings u;
            int thID = Thread.CurrentThread.ManagedThreadId;

            /*Controllo se utente valido*/
            if (!isUserValid(s.user, Path.GetInvalidFileNameChars())) return false;

            foreach (Syncro sync in executingSync)
            {
                if (sync.syncSettings.user == s.user)
                {
                    Console.WriteLine("(" + thID + ") ->Utente {0} già autenticato su questo server",  s.user);
                    return false;
                }
                    
            }

            foreach (Restore rest in executingRestore)
            {
                if (rest.syncSettings.user == s.user)
                {
                    Console.WriteLine("(" + thID + ") ->Utente {0} già autenticato su questo server", s.user);
                    return false;
                }
            }


            try
            {
                users.Open();
                if (!Settings.getSettingsDB(users, s.folder, s.user, s.pwd, out u))
                    return false;

                /*Elmino Dir se utente nuovo ma dir già esistente*/
                if (!u.active)
                {
                    try
                    {
                        DirectoryInfo d = new DirectoryInfo(s.user);
                        if (d.Exists)
                            d.Delete(true);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("(" + thID + ")_ERRORE: impossibile Eliminare directory relativa a vecchio utente {0} ({1})", s.user, e.Message);
                        return false;
                    }
                   
                }

                /*Eliminazione vecchia folder*/
                if (s.folder != u.folder)
                {
                    Console.WriteLine("(" + thID + ") ->Directory differente: {0} per utente {1} ", s.folder, s.user);
                    try
                    {
                        if (cmdtype != Command.type.SYNC_REQ) return false;

                        if (!Settings.delSettingsDB(users, u))
                            return false;

                        DirectoryInfo d = new DirectoryInfo(s.user);
                        if(d.Exists)
                            d.Delete(true);

                        if (!Settings.getSettingsDB(users, s.folder, s.user, s.pwd, out u))
                            return false;

                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("(" + thID + ")_ERRORE: impossibile cambiare directory per utente {0} ({1})", s.user, e.Message);
                        return false;
                    }

                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (users.State == System.Data.ConnectionState.Open)
                {
                    users.Close();
                }
            }

            /*Creazione Directory per Utente se è il Caso*/
            try
            {
                DirectoryInfo dir = new DirectoryInfo(s.user);
                if (!dir.Exists)
                {
                    dir.Create();    
                }
            }
            catch (Exception)
            {
                Console.WriteLine("(" + thID + ")_ERRORE: impossibile creare Directory per l'utente: " + s.user);
                return false;
            }

            /*Creazione DB se è il caso*/
            String nomeDb = s.user + @"\" + s.user + ".sqlite";
            FileInfo db = new FileInfo(nomeDb);
            if (db.Exists)
                return true;

            SQLiteConnection c = null;
            try
            {
                SQLiteConnection.CreateFile(nomeDb);
                c = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;");

                /*Creo Tabella Necessaria*/
                String sql = "CREATE TABLE files (id INTEGER PRIMARY KEY AUTOINCREMENT, name VARCHAR(248), directory VARCHAR(248), size INTEGER, checksum TEXT, syncData DATETIME, version INTEGER, deleted BOOLEAN)";
                SQLiteCommand cmd = new SQLiteCommand(sql, c);
                c.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                Console.WriteLine("(" + thID + ")_ERRORE: impossibile creare il DB per l'utente: " + s.user);
                return false;
            }
            finally
            {
                if (c != null && c.State == System.Data.ConnectionState.Open)
                    c.Close();

            }
            return true;
        }

        /// <summary>
        /// Metodo che si occupa di inizializzare il DB per gli utenti
        /// se esiste già crea una connessione, altrimenti prima crea il DB e poi apre la connessione
        /// </summary>
        /// <returns>Connessione da APRIRE al db SQLite</returns>
        private SQLiteConnection initDbUsers()
        {
            FileInfo f = new FileInfo(usersDb);
            if (f.Exists)
            {
                try
                {
                    return new SQLiteConnection("Data Source=" + usersDb + ";Version=3;");
                }
                catch
                {
                    Console.WriteLine("\t-> ERRORE: DB utenti Corrotto!");
                    return null;
                }
            }

            /*Creo DB necessario*/
            Console.WriteLine("\tCreo Nuovo Database Utenti...");
            SQLiteConnection c = null;
            try
            {
                SQLiteConnection.CreateFile(usersDb);
                c = new SQLiteConnection("Data Source=" + usersDb + ";Version=3;");

                /*Creo Tabella Necessaria*/
                String sql = "CREATE TABLE utenti (nome VARCHAR(20), password VARCHAR(30), folder VARCHAR(248))";
                SQLiteCommand cmd = new SQLiteCommand(sql, c);
                c.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                Console.WriteLine("\t_ERRORE: Al momento non è possibile Creare un nuovo DB per gli utenti");
                return null;
            }
            finally
            {
                if (c != null && c.State == System.Data.ConnectionState.Open)
                    c.Close();
            }


            return c;

        }

        /// <summary>
        /// Proprietà che indica se i thread devono terminare
        /// </summary>
        public bool shouldStop
        {
            set 
            { 
                _shouldStop = value;
                
                if (executingSync != null)
                    foreach (Syncro s in executingSync)
                        s.shouldStop = value;
               
                if (executingRestore != null)
                    foreach (Restore r in executingRestore)
                        r.shouldStop = value;

            }
            get { return _shouldStop; }
        }

        /// <summary>
        /// Metodo che si occupa di attendere i Thread
        /// </summary>
        public void waitForThreads()
        {
            foreach (Thread t in threadList)
            {
                t.Join();
            }
        }

        /// <summary>
        /// Metodo che Controlla se l'username è un valore valido
        /// </summary>
        /// <param name="user">User Passato come Parametro</param>
        /// <param name="invalidChars">Array contenente i caratteri non validi</param>
        /// <returns>True se Valido False se non</returns>
        static public bool isUserValid(String user, char[] invalidChars)
        {
            return user.IndexOfAny(invalidChars) == -1;
        }

    }
}
