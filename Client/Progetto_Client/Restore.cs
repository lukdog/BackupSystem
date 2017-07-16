using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Security;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Net.Sockets;
using System.Net;

namespace Progetto_Client
{
    
    /// <summary>
    /// Delegato che viene richiamato per popolare una TreeView a partire da una collezione di Path
    /// </summary>
    public delegate void pathToTree(TreeView tree, IEnumerable<String> paths, char pathSeparator);

    /// <summary>
    /// Delegato che viene chiamato alla fine di un'attività
    /// </summary>
    public delegate void finishActivity();
    
    /// <summary>
    /// Delegato che viene chiamato quando si vuole popolare una ListBox
    /// </summary>
    public delegate void versionToList(ListBox box, IEnumerable<Version> versions);
    
    /// <summary>
    /// Classe che si occupa di eseguire le attività utili per il ripristino di file o Directory
    /// </summary>
     class Restore
    {
        private Thread masterThread = null;
        private volatile bool _shouldStop = false;
        
        private Settings settings = null;
        private TcpClient sock = null;
        private NetworkStream netStream = null;
        private const int readTimeout = 10000;
        private const int writeTimeout = 10000;

        private Double updForFile = 0;
        private Double prog = 0;

        /*Delegati o Eventi*/
        public event errWriter writeErr;
        public event pathToTree populateTree;
        public event finishActivity populatedTree;
        public event versionToList populateList;
        public event finishActivity populatedList;
        public event finishActivity restored;
        public event updValue updProg;
        public event msgWriter stateWrite;

        /// <summary>
        /// Costruttore della Classe Restore
        /// </summary>
        /// <param name="s">Settings della sincronizzazione</param>
        public Restore(Settings s)
        {
            settings = s;
        }

        /// <summary>
        /// Proprietà che memorizza il thread principale che esegue questa classe
        /// </summary>
        public Thread master
        {
            get { return masterThread; }
        }

        /// <summary>
        /// Proprietà che permette di settare lo stop della sincronizzazione e quindi la chiusura dei Thread
        /// </summary>
        public bool shouldStop
        {
            get { return _shouldStop; }
            set { _shouldStop = value; }
        }

        /// <summary>
        /// Metodo che si occupa di avviare il Thread che popola la TreeView Passata come parametro
        /// </summary>
        /// <param name="t">TreeView Da popolare</param>
        /// <returns></returns>
        public bool retrieveFiles(TreeView t)
        {
            ParameterizedThreadStart todo = new ParameterizedThreadStart(composeTree);
            try
            {
               masterThread = new Thread(todo);
               masterThread.Start(t);
            }
            catch (OutOfMemoryException)
            {
                sendErrFatal("Impossibile Avviare la Sincronizzazione in questo momento, non ci sono abbastanza risorse di sistema diponibili");
                return false;
            }

            return true;
          
        }

        /// <summary>
        /// Metodo che si occupa di comunicare con il server e di popolare la TreeView
        /// </summary>
        /// <param name="o">Parametro di Tipo TreeView</param>
        private void composeTree(Object o)
        {
            
            TreeView tree = (TreeView)o;
            if (!initConnection()) return;
            
            /*Invio Comando LIST_REQ*/
            Command req = new Command(Command.type.LIST_REQ);
            req.settings = settings;
            if (!req.serializeTo(netStream))
            {
               sendErrFatal("Impossibile Inviare la richiesta al server");
               return;
            }

            /*Attendo risposta con lista*/
            Command res = Command.deserializeFrom(sock, netStream, ref _shouldStop);
            if (_shouldStop)
                return;
            else if (res == null)
            {
                sendErrFatal("Nessuna Risposta Ricevuta dal Server");
                return;
            }
            else if (res.cmd == Command.type.ERRPSW)
            {
                sendErrFatal("Impossibile Eseguire l'Autenticazione con successo, controlla username e password, e la directory di cui si vuole fare il ripristino.");
                return;
            }
            else if (res.cmd != Command.type.LIST_RES)
            {
                sendErrFatal("Il Server ha Riscontrato un Errore");
                return;
            }
            else if (res.filesPath == null)
            {
                sendErrFatal("Il Server Non ha risposto in modo Corretto");
                return;
            }
            else if (res.filesPath.Count == 0)
            {
                sendErrFatal("Non hai nessun file Sincronizzato su questo Server, devi prima effettuare una sincronizzazione");
                return;
            }

            /*Compongo Tree*/
            if (populateTree != null)
            {
                if (tree.Dispatcher.CheckAccess())
                    populateTree(tree, res.filesPath, '\\');
                else
                    tree.Dispatcher.Invoke(populateTree, tree, res.filesPath, '\\');
            }

            if (populatedTree != null)
                populatedTree();

            /*Chiudo la Connessione*/
            netStream.Close();
            sock.Close();
        }

        /// <summary>
        /// Metodo che si occupa di avviare il Thread che popola la lista passata come parametro
        /// </summary>
        /// <param name="t">TreeView Da popolare</param>
        /// <param name="v">Oggetto versione da richiedere al server</param>
        /// <returns>True se Eseguita correttamente</returns>
        public bool retrieveVersions(ListBox t, Version v)
        {
            ParameterizedThreadStart todo = new ParameterizedThreadStart(composeList);
            try
            {
                masterThread = new Thread(todo);
                masterThread.Start(new dictElement<Version, ListBox>(v, t));
            }
            catch (OutOfMemoryException)
            {
                sendErrFatal("Impossibile Avviare la Sincronizzazione in questo momento, non ci sono abbastanza risorse di sistema diponibili");
                return false;
            }

            return true;

        }

        /// <summary>
        /// Metodo che si occupa di comunicare con il server e di popolare la lista di versioni
        /// </summary>
        /// <param name="o">Oggetto contenete la TreeView e la Stringa rappresentante il path</param>
        private void composeList(Object o)
        {
            ListBox list = ((dictElement<Version, ListBox>)o).value;
            Version path = ((dictElement<Version, ListBox>)o).key;
            if (!initConnection()) return;

            /*Invio Comando VERS_REQ*/
            Command req = new Command(Command.type.VERS_REQ);
            req.settings = settings;
            req.versions = new List<Version>();
            req.versions.Add(path);
            if (!req.serializeTo(netStream))
            {
                sendErrFatal("Impossibile Inviare la richiesta al server");
                return;
            }

            /*Attendo risposta con lista*/
            Command res = Command.deserializeFrom(sock, netStream, ref _shouldStop);
            if (_shouldStop)
                return;
            else if (res == null)
            {
                sendErrFatal("Nessuna Risposta Ricevuta dal Server");
                return;
            }
            else if (res.cmd == Command.type.ERRPSW)
            {
                sendErrFatal("Impossibile Eseguire l'Autenticazione con successo, controlla username e password, e la directory di cui si vuole fare il ripristino.");
                return;
            }
            else if (res.cmd != Command.type.VERS_LIST)
            {
                sendErrFatal("Il Server ha Riscontrato un Errore");
                return;
            }
            else if (res.versions == null)
            {
                sendErrFatal("Il Server Non ha risposto in modo Corretto");
                return;
            }

            /*Compongo List*/
            if (populateList != null)
            {
                if (list.Dispatcher.CheckAccess())
                    populateList(list, res.versions);
                else
                    list.Dispatcher.Invoke(populateList, list, res.versions);
            }

            if (populatedList != null)
                populatedList();

            /*Chiudo la Connessione*/
            netStream.Close();
            sock.Close();

        }

        /// <summary>
        /// Metodo che si occupa di avviare il Thread che effettua il ripristino del file
        /// </summary>
        /// <param name="v">Versione da Ripristinare</param>
        /// <returns>True se avviato Correttamente</returns>
        public bool retrieveVersion(Version v)
        {
            ParameterizedThreadStart todo = new ParameterizedThreadStart(restoreFiles);
            try
            {
                masterThread = new Thread(todo);
                masterThread.Start(v);
            }
            catch (OutOfMemoryException)
            {
                sendErrFatal("Impossibile Avviare la Sincronizzazione in questo momento, non ci sono abbastanza risorse di sistema diponibili");
                return false;
            }

            return true;

        }

        /// <summary>
        /// Metodo Eseguito dal Thread che si occupa di ripristinare i file ricevuti dal server
        /// </summary>
        /// <param name="o">Versione da ripristinare</param>
        private void restoreFiles(Object o)
        {
            Version v = (Version)o;
            if (v.numberOfFiles == 0)
            {
                if (restored != null) restored();
                return;
            }

            if (!initConnection()) return;

            /*Avvio Transizione su Directory*/
            DirTransaction tr = null;
            try
            {
                tr = new DirTransaction(settings.folder);
                if(v.isDirectory)
                    DirTransaction.cleanDir(new DirectoryInfo(v.path));
                else
                {
                    FileInfo f = new FileInfo(v.path);
                    if(f.Exists)
                        f.Delete();
                }
            }
            catch (Exception)
            {
                sendErrFatal("Impossibile Avviare il Ripristino dei file a causa di un errore imprevisto");
                return;
            }
            
            /*Invio Comando REST_REQ*/
            Command req = new Command(Command.type.REST_REQ);
            req.settings = settings;
            req.versions = new List<Version>();
            req.versions.Add(v);

            if (!req.serializeTo(netStream))
            {
                sendErrFatal("Impossibile Inviare la richiesta al server");
                netStream.Close();
                sock.Close();
                return;
            }


            if (_shouldStop)
            {
                netStream.Close();
                sock.Close();
                return;
            }

            if (updProg != null) updProg(0);
            updForFile = 100.0 / v.numberOfFiles;

            recvFiles(tr);

            /*Chiudo la Connessione*/
            netStream.Close();
            sock.Close();
        }

        /// <summary>
        /// Metodo che si occupa di ricevere i files dal server
        /// </summary>
        /// <param name="tr">Transazione attiva sulla directory</param>
        private void recvFiles(DirTransaction tr)
        {
            bool someError = false;
            /*Ricevo comando dal server*/
            Command c = Command.deserializeFrom(sock, netStream, ref _shouldStop);
            if(!_shouldStop && c == null)
            {
                sendErrFatal("Il Server non ha risposto, impossibile Ripristinare i Files in questo momento");
                tr.rollback();
                return;
            }
            else if (_shouldStop)
            {
                tr.rollback();
                return;
            }
            else if (c.cmd == Command.type.ERRPSW)
            {
                sendErrFatal("Impossibile Eseguire l'Autenticazione con successo, controlla username e password, e la directory di cui si vuole fare il ripristino.");
                return;
            }

            try
            {
                /*Ricevo files fino a quando non ricevo il comando END*/
                while (c.cmd != Command.type.END && !_shouldStop)
                {
                    if (c.cmd == Command.type.NEW)
                    {
                        
                        if (!recvFileByte(c.file))
                        {
                            tr.rollback();
                            Command err = new Command(Command.type.ERR);
                            err.serializeTo(netStream);
                            if(!_shouldStop)
                                sendErrFatal("Impossibile Ripristinare i files in questo momento, problemi durante la comunicazione con il server");
                            someError = true;
                            break;
                        }

                        if (_shouldStop) break;
                        prog += updForFile;
                        if (updProg != null) updProg(prog);

                        /*Comando SUCC per confermare successo*/
                        Command succ = new Command(Command.type.SUCC);
                        succ.serializeTo(netStream);

                        /*Ricevo nuovo Comando*/
                        c = Command.deserializeFrom(sock, netStream, ref _shouldStop);
                        if (_shouldStop) break;
                        else if (c == null)
                        {
                            sendErrFatal("Impossibile Ripristinare i files in questo momento, il server non risponde");
                            tr.rollback();
                            someError = true;
                            break;
                        }

                    }
                    else
                    {
                        tr.rollback();
                        Command err = new Command(Command.type.ERR);
                        err.serializeTo(netStream);
                        sendErrFatal("Impossibile Ripristinare i files in questo momento, il Server non risponde in modo corretto");
                        someError = true;
                        break;
                    }
                }

                if (_shouldStop)
                    tr.rollback();
                else if (!someError)
                    tr.commit();

            }
            catch (Exception)
            {
                sendErrFatal("Impossibile Ripristinare i files in questo momento.");
                tr.rollback();
                return;
            }
            if (_shouldStop)
            {
                sendErr("Ripristino Annullato");
                return;
            }
            
            if (updProg != null) updProg(100);
            if (stateWrite != null) stateWrite("Files Ripristinati");
            if (restored != null)
                restored();
        }

        /// <summary>
        /// Metodo che Si occupa di ricevere effettivamente il file dallo stream indicato
        /// </summary>
        /// <param name="f">Attributi del file da ricevere</param>
        /// <returns>True se Ricevuto Correttamente</returns>
        private bool recvFileByte(FileAttr f)
        {
            if (f == null) return false;

            if (stateWrite != null)
            {
                String state = "Ripristino: " + f.path;
                if (state.Length > 124)
                {
                    state = state.Substring(0, 120);
                    state += "...";
                }
                stateWrite(state);
            }

            DirectoryInfo d = Directory.CreateDirectory(f.directory);
            if (!d.Exists) return false;
            
            Stream file = File.Open(f.path, FileMode.Create);
            int tot = 0, rec = 0;
            long diff;
            Byte[] bu = new Byte[1024];

            try
            {
                int i = 0;
                while (tot != f.size && !_shouldStop)
                {

                    try
                    {
                        if ((diff = f.size - tot) < 1024)
                        {
                            rec = netStream.Read(bu, 0, (int)diff);
                        }
                        else
                        {
                            rec = netStream.Read(bu, 0, 1024);
                        }
                        if (rec <= 0) return false;
                        tot += rec;
                        file.Write(bu, 0, rec);
                    }
                    catch
                    {
                        if (!sock.Connected) return false;
                        i++;
                        if (i >= 50) i++;
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                file.Close();
            }

            if (_shouldStop && f.size != tot)
                return false;

            return true;
        }

        /// <summary>
        /// Metodo Privato per stabilire la connessione con il server
        /// </summary>
        /// <returns>True se connessione stabilita correttamente</returns>
        private bool initConnection()
        {
            try
            {
                IPAddress server = IPAddress.Parse(settings.server);
                IPEndPoint endP = new IPEndPoint(server, (Int32)settings.port);
                sock = new TcpClient();
                sock.Connect(endP);
                netStream = sock.GetStream();
                netStream.ReadTimeout = readTimeout;
                netStream.WriteTimeout = writeTimeout;
            }
            catch (FormatException)
            {
                sendErrFatal("Indirizzo IP specificato non Valido");
                _shouldStop = true;
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                sendErrFatal("Indirizzo IP o porta specificati fuori dal Range Consentito");
                _shouldStop = true;
                return false;
            }
            catch (InvalidOperationException)
            {
                sendErrFatal("Impossibile Connetersi al Server Specificato");
                _shouldStop = true;
                if (netStream != null) netStream.Close();
                if (sock != null) sock.Close();
                return false;
            }
            catch (SocketException)
            {
                sendErrFatal("Impossibile Connetersi al Server Specificato");
                _shouldStop = true;
                if (netStream != null) netStream.Close();
                if (sock != null) sock.Close();
                return false;
            }
            catch (Exception e)
            {
                sendErrFatal("Impossibile stabilire connessione: " + e.Message);
                _shouldStop = true;
                if (netStream != null) netStream.Close();
                if (sock != null) sock.Close();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Metodo che tramite l'evento writeErr invia un errore Fatale
        /// </summary>
        /// <param name="msg">Testo dell'Errore</param>
        private void sendErrFatal(String msg)
        {
            if (writeErr != null)
                writeErr(msg, true);

            if (netStream != null) netStream.Close();
            if (sock != null) sock.Close();
        }

        /// <summary>
        /// Metodo che tramite l'evento writeErr invia un errore
        /// </summary>
        /// <param name="msg">Testo dell'Errore</param>
        private void sendErr(String msg)
        {
            if (writeErr != null)
                writeErr(msg, false);
        }

    }
}
