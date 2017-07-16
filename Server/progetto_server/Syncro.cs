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
    /// <summary>
    /// Classe che implementa i metodi per la sincronizzazione dei file tra client e server
    /// </summary>
    class Syncro
    {
        private TcpClient client = null;
        private NetworkStream stream = null;
        private Settings settings = null;
        private DateTime time;
        private int thID = 0;
        private volatile bool _shouldStop = false;
        private String nomeDb;

        /// <summary>
        /// Costruttore della Classe Syncro, imposta i parametri utili per la sincronizzazione
        /// </summary>
        /// <param name="c">oggetto TcpClient relativo al client con cui si vuole comunicare</param>
        /// <param name="s">Settings relative alla sincronizzazione Attuale</param>
        public Syncro(TcpClient c, Settings s)
        {
            client = c;
            settings = s;
            stream = client.GetStream();
            stream.ReadTimeout = Server.readingTimeout;
            stream.WriteTimeout = Server.writingTimeout;
            thID = Thread.CurrentThread.ManagedThreadId;
            nomeDb = settings.user + @"\" + settings.user + ".sqlite";
            time = DateTime.Now;
        }

        /// <summary>
        /// Metodo che avvia la sincronizzazione vera e propria
        /// </summary>
        public void start()
        {
            /*Invio Risposta con dizionario file presenti*/
            ConcurrentDictionary<String, FileAttr> dic = getFileList();
            if (_shouldStop)
            {
                Command err = new Command(Command.type.ERR);
                err.serializeTo(stream);
                stream.Close();
                client.Close();
                return;
            }
            else if (dic == null)
            {
                Console.WriteLine("(" + thID + ") -> Impossibile definire l'elenco file per l'utente " + settings.user);
                Command err = new Command(Command.type.ERR);
                err.serializeTo(stream);
                stream.Close();
                client.Close();
                return;
            }

            Command res = new Command(Command.type.SYNC_RES);
            res.dictionary = dic;
            res.serializeTo(stream);
            Console.WriteLine("(" + thID + ") -> Inviato Elenco File per utente " + settings.user);

            /*Ricevo i File*/
            syncAllFiles();
        }

        /// <summary>
        /// Metodo che crea il Dictionary a partire dai file memorizzati nel DB, preleva solo i record con versione 0 e non cancellati.
        /// </summary>
        /// <returns>Il Dictionary se possibile, altrimenti null</returns>
        private ConcurrentDictionary<String, FileAttr> getFileList()
        {
            ConcurrentDictionary<String, FileAttr> dic = new ConcurrentDictionary<String, FileAttr>();
            SQLiteConnection db = null;

            try
            {
                db = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;");
                String sql = "SELECT name, size, checksum, directory FROM files WHERE version=0 AND deleted='FALSE'";
                db.Open();
                SQLiteCommand cmd = new SQLiteCommand(sql, db);
                SQLiteDataReader r = cmd.ExecuteReader();

                while (r.Read() && !_shouldStop)
                {
                    FileAttr fa = new FileAttr(r.GetString(0), r.GetString(3), r.GetString(2), r.GetInt32(1));
                    if (!dic.TryAdd(fa.path, fa))
                    {
                        Console.WriteLine("ERRORE " + fa.path);
                        return null;
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            finally
            {
                if (db != null && db.State == System.Data.ConnectionState.Open)
                    db.Close();
            }

            return dic;
        }

        /// <summary>
        /// Metodo che si occupa di ricevere i comandi dall'utente, interpretarli e aggiornare il DB dell'utente.
        /// </summary>
        private void syncAllFiles()
        {
            Boolean someError = false;
            try
            {
                using (SQLiteConnection db = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;"))
                {
                    try
                    {
                        db.Open();

                        using (SQLiteTransaction tr = db.BeginTransaction())
                        {
                            try
                            {
                                /*Ricevo Comandi*/
                                Command c = Command.deserializeFrom(client, stream, ref _shouldStop);
                                if (!_shouldStop && c == null)
                                {
                                    Console.WriteLine("(" + thID + ") _ERRORE: Nessun comando ricevuto dall'utente:" + settings.user);
                                    someError = true;
                                }
                                else if (!_shouldStop)
                                {
                                    while (c.cmd != Command.type.END && !_shouldStop)
                                    {
                                        /*Interpreto Comando*/
                                        if (c.cmd == Command.type.NEW)
                                        {
                                            /*Nuovo File*/
                                            Console.WriteLine("(" + thID + ") -> Nuovo File da utente {0} : {1}", settings.user, c.file);
                                            if (!syncNewFile(c.file, db, true, tr))
                                            {
                                                tr.Rollback();
                                                Command err = new Command(Command.type.ERR);
                                                err.serializeTo(stream);
                                                someError = true;
                                                break;
                                            }
                                        }
                                        else if (c.cmd == Command.type.UPD)
                                        {
                                            /*Update di File*/
                                            Console.WriteLine("(" + thID + ") -> Aggiornamento File da utente {0} : {1}", settings.user, c.file);
                                            if (!syncNewFile(c.file, db, true, tr))
                                            {
                                                tr.Rollback();
                                                Command err = new Command(Command.type.ERR);
                                                err.serializeTo(stream);
                                                someError = true;
                                                break;
                                            }
                                
                                        }
                                        else if (c.cmd == Command.type.DEL)
                                        {
                                            /*Eliminazione di File*/
                                            Console.WriteLine("(" + thID + ") -> Eliminazione File da utente {0} : {1}", settings.user, c.file);
                                            if (!delFile(c.file, db, tr))
                                            {
                                                tr.Rollback();
                                                Command err = new Command(Command.type.ERR);
                                                err.serializeTo(stream);
                                                someError = true;
                                                break;
                                            }
                                     
                                        }
                                        else
                                        {
                                            /*Ricevuto Comando non Valido, invio ERR*/
                                            tr.Rollback();
                                            Command err = new Command(Command.type.ERR);
                                            err.serializeTo(stream);
                                            someError = true;
                                            break;
                                        }

                                        /*Comando SUCC per confermare successo*/
                                        Command succ = new Command(Command.type.SUCC);
                                        succ.serializeTo(stream);

                                        /*Ricevo nuovo Comando*/
                                        c = Command.deserializeFrom(client, stream, ref _shouldStop);
                                        if (c == null)
                                        {
                                            tr.Rollback();
                                            Console.WriteLine("(" + thID + ") _ERRORE: Nessun comando ricevuto dall'utente:" + settings.user);
                                            someError = true;
                                            break;
                                        }
                                    }

                                    if (!someError)
                                    {
                                        /*Commit Della Tranzazione*/
                                        tr.Commit();

                                        /*Elimino Versioni troppo vecchie se il server è in chiusura non vengono eliminate, ma saranno comunque eliminate sucessivamente*/
                                        if(!_shouldStop)
                                            cleanVersion(db);
                                    }

                                }

                                if (db != null && db.State == System.Data.ConnectionState.Open)
                                    db.Close();
                            }
                            catch
                            {
                                tr.Rollback();
                                if (db != null && db.State == System.Data.ConnectionState.Open)
                                    db.Close();
                                Command err = new Command(Command.type.ERR);
                                err.serializeTo(stream);
                                return;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        if (db != null && db.State == System.Data.ConnectionState.Open)
                            db.Close();
                        Command err = new Command(Command.type.ERR);
                        err.serializeTo(stream);
                        return;
                    }
                }
            }
            catch
            {
                Console.WriteLine("(" + thID + ") _ERRORE: Impossibile connettersi al DB durante la sincronizzazione dei file per l'utente " + settings.user);
                return;
            }

            if (_shouldStop)
            {
                Command err = new Command(Command.type.ERR);
                err.serializeTo(stream);
            }

            return;
        }

        /// <summary> 
        /// Metodo che si occupa di Aggiungere un file al DB dell'utente
        /// </summary>
        /// <param name="f">Attributi del file da aggiungere</param>
        /// <param name="db">Connessione APERTA al DB dell'utente</param>
        /// <param name="upd">True se un Aggiornamento</param>
        /// <param name="tr">Transazione sulla quale vengono eseguiti i comandi</param>
        /// <returns>True se file Salvato Correttamente</returns>
        private bool syncNewFile(FileAttr f, SQLiteConnection db, Boolean upd, SQLiteTransaction tr)
        {
            if (f == null) return false;
          
            String sql = "INSERT INTO files(name, directory, size, checksum, syncData, version, deleted) VALUES(@name, @dir, @size, @checksum, @syncData, @version, 'FALSE')";
            /*Inizio Transazione per salvataggio File*/
            try
            {
                /*Se Aggiornamento Incremento ID dei File*/
                if (upd)
                {
                    bool ctrlExist = false;
                    String ctrlEx = "SELECT * FROM files WHERE name=@name";
                    using (SQLiteCommand ctrl = new SQLiteCommand(ctrlEx, db, tr))
                    {
                        ctrl.Prepare();
                        ctrl.Parameters.AddWithValue("@name", f.path);
                        SQLiteDataReader r = ctrl.ExecuteReader();
                        if (r.HasRows) ctrlExist = true;
                    }

                    if (ctrlExist) 
                    {
                        String updQ = "UPDATE files SET version = version +1 WHERE name=@name";
                        using (SQLiteCommand updC = new SQLiteCommand(updQ, db, tr))
                        {
                            updC.Prepare();
                            updC.Parameters.AddWithValue("@name", f.path);
                            if (updC.ExecuteNonQuery() < 1)
                            {
                                Console.WriteLine("(" + thID + ")_ERRORE: impossibile modificare la versione file nel DB dell'utente : " + settings.user);
                                return false;
                            }
                        }
                    }
                    
                }

                /*Aggiungo nuovo Record e Salvo nuovo File*/
                using (SQLiteCommand c = new SQLiteCommand(sql, db, tr))
                {
                    c.Prepare();
                    c.Parameters.AddWithValue("@name", f.path);
                    c.Parameters.AddWithValue("@dir", f.directory);
                    c.Parameters.AddWithValue("@size", f.size);
                    c.Parameters.AddWithValue("@checksum", f.checksum);
                    c.Parameters.AddWithValue("@syncData", DateTimeSQLite(time));
                    c.Parameters.AddWithValue("@version", 0);
                    if (c.ExecuteNonQuery() != 1)
                    {
                        Console.WriteLine("(" + thID + ")_ERRORE: impossibile aggiungere record al DB dell'utente : " + settings.user);
                        return false;
                    }
                }

                /*Ricavo l'id del nuovo File*/
                int id = 0;
                using (SQLiteCommand c = new SQLiteCommand("SELECT * FROM files", db, tr))
                {
                    SQLiteDataReader r = c.ExecuteReader();
                    if (r.HasRows)
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("SELECT MAX(id) AS max FROM files", db, tr))
                        {
                            r = cmd.ExecuteReader();
                            r.Read();
                            id = r.GetInt32(0);
                        }

                    }
                }

                /*Ricevo File*/
                if (!recvFileByte(id, f))
                {
                    Console.WriteLine("(" + thID + ")_ERRORE: impossibile ricevere il contenuto del file dell'utente : " + settings.user);
                    return false;
                }

            }
            catch (Exception e)
            {
                if (tr != null)
                    tr.Rollback();
                Console.WriteLine("(" + thID + ")_ERRORE: Impossibile sincronizzare il file {0} dell'utente {1}\n\t({2})", f.path, settings.user, e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Metodo che si occupa di Settare come Eliminato un file nel DB dell'utente
        /// </summary>
        /// <param name="f">File da Eliminare</param>
        /// <param name="db">Connessione APERTA al DB dell'utente</param>
        /// <param name="tr">Transazione all'interno della quale eseguire i comandi</param>
        /// <returns>True se Record salvato correttamente</returns>
        private bool delFile(FileAttr f, SQLiteConnection db, SQLiteTransaction tr)
        {
            if (f == null) return false;
            int thID = Thread.CurrentThread.ManagedThreadId;

            try
            {
                String updQ = "UPDATE files SET version = version +1 WHERE name=@name";
                using (SQLiteCommand updC = new SQLiteCommand(updQ, db, tr))
                {
                    updC.Prepare();
                    updC.Parameters.AddWithValue("@name", f.path);
                    if (updC.ExecuteNonQuery() < 1)
                    {
                        Console.WriteLine("(" + thID + ")_ERRORE: impossibile modificare la versione file nel DB dell'utente : " + settings.user);
                        return false;
                    }
                }

                String sqlN = "INSERT INTO files(name, directory, size, checksum, syncData, version, deleted) VALUES(@name, @dir, @size, @checksum, @syncData, @version, 'TRUE')";
                /*Aggiungo nuovo Record*/
                using (SQLiteCommand c = new SQLiteCommand(sqlN, db, tr))
                {
                    c.Prepare();
                    c.Parameters.AddWithValue("@name", f.path);
                    c.Parameters.AddWithValue("@dir", f.directory);
                    c.Parameters.AddWithValue("@size", f.size);
                    c.Parameters.AddWithValue("@checksum", f.checksum);
                    c.Parameters.AddWithValue("@syncData", DateTimeSQLite(time));
                    c.Parameters.AddWithValue("@version", 0);
                    if (c.ExecuteNonQuery() != 1)
                    {
                        Console.WriteLine("(" + thID + ")_ERRORE: impossibile aggiungere record al DB dell'utente : " + settings.user);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {

                Console.WriteLine("(" + thID + ")_ERRORE: Impossibile sincronizzare il file {0} dell'utente {1}\n\t({2})", f.path, settings.user, e.Message);
                return false;
            }

            return true;

        }

        /// <summary>
        /// Metodo che Si occupa di ricevere effettivamente il file dallo stream indicato
        /// </summary>
        /// <param name="id">ID univoco del file</param>
        /// <param name="f">Attributi del file da ricevere</param>
        /// <returns>True se Ricevuto Correttamente</returns>
        private bool recvFileByte(int id, FileAttr f)
        {
            String nomeFile = settings.user + @"\" + id + ".bck";
            if (f == null) return false;
            Stream file = File.Open(nomeFile, FileMode.Create);
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
                            rec = stream.Read(bu, 0, (int)diff);
                        }
                        else
                        {
                            rec = stream.Read(bu, 0, 1024);
                        }
                        if (rec <= 0) return false;
                        tot += rec;
                        file.Write(bu, 0, rec);
                    }
                    catch
                    {
                        if (!client.Connected) return false;
                        i++;
                        if (i >= 20) return false;
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
        /// Metodo che si occupa di Eliminare le Versioni Troppo vecchie dei file Sincronizzati
        /// </summary>
        /// <param name="db">Connessione al DB sqlite Aperta</param>
        private void cleanVersion(SQLiteConnection db)
        {
            String sqlR = String.Format("SELECT id FROM files WHERE version > '{0}'", Server.maxVersion);
            String sqlD = String.Format("DELETE FROM files WHERE version > '{0}'", Server.maxVersion);

            /*Cancello il File Dal Disco*/
            using (SQLiteCommand c = new SQLiteCommand(sqlR, db))
            {
                SQLiteDataReader reader = c.ExecuteReader();

                while (reader.Read())
                {
                    String nomefile = settings.user + @"\" + reader.GetInt32(0) + ".bck";

                    FileInfo f = new FileInfo(nomefile);
                    f.Delete();
                    
                }
            }

            /*Elimino il Record dal DB*/
            using (SQLiteCommand c = new SQLiteCommand(sqlD, db))
            {
                c.ExecuteNonQuery();
            }

        }

        /// <summary>
        /// Proprietà che setta la terminazione dei Task in Esecuzione
        /// </summary>
        public bool shouldStop
        {
            set{_shouldStop = value;}
        }

        /// <summary>
        /// Proprietà che permette di leggere le impostazioni di sincronizzazione
        /// </summary>
        public Settings syncSettings
        {
            get { return settings; }
        }

        /// <summary>
        /// Metodo che Formatta il DateTime nel formato SQLite
        /// </summary>
        /// <param name="datetime">DateTime da Formattare</param>
        /// <returns>Stringa con DateTime Formattato</returns>
        private static string DateTimeSQLite(DateTime datetime)
        {
            string dateTimeFormat = "{0:0000}-{1:00}-{2:00} {3:00}:{4:00}:{5:00}.{6:000}";
            return string.Format(dateTimeFormat, datetime.Year, datetime.Month, datetime.Day, datetime.Hour, datetime.Minute, datetime.Second, datetime.Millisecond);
        }

    }
}
