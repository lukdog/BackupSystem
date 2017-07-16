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
    class Restore
    {
        private TcpClient client = null;
        private NetworkStream stream = null;
        private Settings settings = null;
        private int thID = 0;
        private volatile bool _shouldStop = false;
        private String nomeDb;

        public Restore(TcpClient c, Settings s)
        {
            client = c;
            settings = s;
            stream = client.GetStream();
            stream.ReadTimeout = Server.readingTimeout;
            stream.WriteTimeout = Server.writingTimeout;
            thID = Thread.CurrentThread.ManagedThreadId;
            nomeDb = settings.user + @"\" + settings.user + ".sqlite";
        }

        /// <summary>
        /// Metodo che si occupa di inviare al Client l'elenco dei paths disponibile per il restore
        /// </summary>
        public void sendListFiles()
        {

            List<String> paths = getPathsList();
            if (_shouldStop || paths == null)
            {
                Command err = new Command(Command.type.ERR);
                err.serializeTo(stream);
                stream.Close();
                client.Close();
                return;
            }
            
            /*Invio Comando al Client con lista*/
            Command res = new Command(Command.type.LIST_RES);
            res.filesPath = paths;
            res.serializeTo(stream);
            Console.WriteLine("(" + thID + ") -> Inviato Elenco File per utente " + settings.user);

        }

        /// <summary>
        /// Metodo che si occupa di Inviare al Client l'elenco delle versioni per un determinato path
        /// </summary>
        /// <param name="path">Percorso di cui vengono richieste le Versioni</param>
        /// <param name="isDir">Bool che specifica se il path corrisponde ad una dir</param>
        public void sendVersions(String path, bool isDir)
        {
            List<Version> versions = getLastVersions(path, isDir);
            if (_shouldStop ||versions == null)
            {
                Command err = new Command(Command.type.ERR);
                err.serializeTo(stream);
                stream.Close();
                client.Close();
                return;
            }

            /*Invio Comando al Client con lista*/
            Command res = new Command(Command.type.VERS_LIST);
            res.versions = versions;
            res.serializeTo(stream);
            Console.WriteLine("(" + thID + ") -> Inviato Elenco Versioni per utente " + settings.user);
        }

        /// <summary>
        /// Metodo che si occupa di inviare i file da ripristinare al Client
        /// </summary>
        /// <param name="v">versione da Ripristinare</param>
        public void sendFiles(Version v)
        {
            List<dictElement<int, FileAttr>> list = getFilesOfVersion(v);
            Console.WriteLine("(" + thID + ") -> Invio File all'utente {0}, ci sono {1} file da ripristinare ", settings.user, list.Count);

            /*Invio tutti i files*/
            foreach (dictElement<int, FileAttr> e in list)
            {
                if (_shouldStop)
                {
                    Command err = new Command(Command.type.ERR);
                    err.serializeTo(stream);
                    stream.Close();
                    client.Close();
                    return;
                }

                Command c = new Command(Command.type.NEW);
                c.file = e.value;
                if (!c.serializeTo(stream))
                {
                    _shouldStop = true;
                }
                else if (!sendFileByte(e.key))
                {
                    _shouldStop = true;
                }

                /*Attendo Riposta SUCC*/
                Command res = Command.deserializeFrom(client, stream, ref _shouldStop);
                if (_shouldStop)
                {
                    Command err = new Command(Command.type.ERR);
                    err.serializeTo(stream);
                    stream.Close();
                    client.Close();
                    return;
                }
                else if (res == null || res.cmd != Command.type.SUCC)
                {
                    Command err = new Command(Command.type.ERR);
                    err.serializeTo(stream);
                    stream.Close();
                    client.Close();
                    return;
                }

            }

            Console.WriteLine("(" + thID + ") -> Invio File terminato, invio END all'utente " + settings.user);
            Command end = new Command(Command.type.END);
            end.serializeTo(stream);

            stream.Close();
            client.Close();
        }

        /// <summary>
        /// Metodo che si connette al DB e popola una lista con i path dei file presenti nel DB
        /// </summary>
        /// <returns>Lista contenente i paths</returns>
        private List<String> getPathsList()
        {
            List<String> files = new List<String>();
            /*Creo Lista di files*/
            try
            {
                using (SQLiteConnection db = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;"))
                {
                    try
                    {
                        db.Open();
                        String sql = "SELECT DISTINCT name FROM files";
                        using (SQLiteCommand com = new SQLiteCommand(sql, db))
                        {
                            SQLiteDataReader reader = com.ExecuteReader();
                            while (reader.Read() && !_shouldStop)
                            {
                                String path = reader.GetString(0);
                                int index;
                                if (settings.folder.Substring(settings.folder.Length - 1) == @"\") index = settings.folder.Length;
                                else index = settings.folder.Length + 1;
                                String real = path.Substring(index);
                                files.Add(real);
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco file per l'utente {0} ({1}) ", settings.user, e.Message);
                        return null;
                    }
                    finally
                    {
                        if (db != null && db.State == System.Data.ConnectionState.Open)
                            db.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco file per l'utente {0} ({1}) ", settings.user, e.Message);
                return null;
            }

            return files;

        }

        /// <summary>
        /// Metodo che si connette al DB e popola una lista con le versioni disponibili per un determinato path
        /// </summary>
        /// <param name="path">Percorso per cui vengono richieste le versioni</param>
        /// <param name="isDir">Specifica se il path è relativo ad una dir</param>
        /// <returns>Lista contenente le versioni relative al percorso</returns>
        private List<Version> getLastVersions(String path, bool isDir)
        {
            List<Version> v = new List<Version>();
            try
            {
                using (SQLiteConnection db = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;"))
                {
                    try
                    {
                        db.Open();
                        String sql;
                        
                        if(isDir)
                            sql = String.Format("SELECT DISTINCT syncData FROM files WHERE name LIKE @name ORDER BY syncData DESC LIMIT {0}", Server.maxVersion);
                        else
                            sql = String.Format("SELECT DISTINCT syncData FROM files WHERE name LIKE @name AND deleted='FALSE' ORDER BY syncData DESC LIMIT {0}", Server.maxVersion);

                        using (SQLiteCommand com = new SQLiteCommand(sql, db))
                        {
                            com.Prepare();
                            com.Parameters.AddWithValue("@name", path + "%");
                            SQLiteDataReader reader = com.ExecuteReader();
                            while (reader.Read() && !_shouldStop)
                            {
                                String data = reader.GetString(0);
                                Version ve = new Version(path);
                                ve.date = data;
                                ve.isDirectory = isDir;
                                ve.numberOfFiles = 1;

                                /*Se è una Directory controllo che ci siano files da ripristinare per questa versione*/
                                if (isDir)
                                {
                                    String ctrlSql = "SELECT COUNT(*) FROM files f WHERE name LIKE @Path AND deleted='FALSE' AND version=(SELECT MIN(version) FROM files f2 WHERE f2.name=f.name AND syncData <= @Data1) AND name IN (SELECT name from files WHERE syncData <= @Data1)";
                                    using (SQLiteCommand ctrl = new SQLiteCommand(ctrlSql, db))
                                    {
                                        ctrl.Prepare();
                                        ctrl.Parameters.AddWithValue("@path", path + "%");
                                        ctrl.Parameters.AddWithValue("@Data1", data);
                                        SQLiteDataReader rCtrl = ctrl.ExecuteReader();
                                        if (rCtrl.Read())
                                        {
                                            if (rCtrl.GetInt32(0) > 0)
                                            {
                                                ve.numberOfFiles = rCtrl.GetInt32(0);
                                                v.Add(ve);
                                            }
                                        }
                                    }
                                }
                                else v.Add(ve);
                                
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco versioni per l'utente {0} e path {2} ({1}) ", settings.user, e.Message, path);
                        return null;
                    }
                    finally
                    {
                        if (db != null && db.State == System.Data.ConnectionState.Open)
                            db.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco versioni per l'utente {0} e path {2} ({1}) ", settings.user, e.Message, path);
                return null;
            }

            return v;


        }

        /// <summary>
        /// Metodo che si connette al DB e popola una lista con tutti i file che fanno parte di una versione
        /// </summary>
        /// <param name="v">Versione da ripristinare</param>
        /// <returns>Lista di coppie ID FileAttr</returns>
        private List<dictElement<int, FileAttr>> getFilesOfVersion(Version v)
        {
            List<dictElement<int, FileAttr>> l = new List<dictElement<int, FileAttr>>();

            try
            {
                using (SQLiteConnection db = new SQLiteConnection("Data Source=" + nomeDb + ";Version=3;"))
                {
                    try
                    {
                        db.Open();
                        String sql;

                        if (v.isDirectory)
                            sql = "SELECT id, name, checksum, size, directory FROM files f WHERE name LIKE @Path AND deleted='FALSE' AND version=(SELECT MIN(version) FROM files f2 WHERE f2.name=f.name AND syncData <= @Data1) AND name IN (SELECT name from files WHERE syncData <= @Data1)";
                        else
                            sql = "SELECT id, name, checksum, size, directory FROM files WHERE name LIKE @Path AND syncData=@Data1";

                        using (SQLiteCommand com = new SQLiteCommand(sql, db))
                        {
                            com.Prepare();
                            com.Parameters.AddWithValue("@Path", v.path + "%");
                            com.Parameters.AddWithValue("@Data1", v.date);
                            SQLiteDataReader reader = com.ExecuteReader();
                            while (reader.Read() && !_shouldStop)
                            {
                                FileAttr f = new FileAttr(reader.GetString(1), reader.GetString(4), reader.GetString(2), reader.GetInt64(3));
                                dictElement<int, FileAttr> el = new dictElement<int, FileAttr>(reader.GetInt32(0), f);
                                l.Add(el);
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco file per l'utente {0} e path {2} ({1}) ", settings.user, e.Message, v.path);
                        return null;
                    }
                    finally
                    {
                        if (db != null && db.State == System.Data.ConnectionState.Open)
                            db.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("(" + thID + ") _ERRORE: impossibile generare elenco versioni per l'utente {0} e path {2} ({1}) ", settings.user, e.Message, v.path);
                return null;
            }

            return l;
        }

        /// <summary>
        /// Metodo che si occupa di inviare il contenuto di un file
        /// </summary>
        /// <param name="id">ID del file memorizzato sul Server</param>
        /// <returns>True se Invio Avvenuto con successo</returns>
        private bool sendFileByte(int id)
        {
            BinaryReader br = null;
            String name = settings.user + @"\" + id + ".bck";
            try
            {
                br = new BinaryReader(File.Open(name, FileMode.Open));
                Byte[] buff = new Byte[1024];
                int toSend;
                while ((toSend = br.Read(buff, 0, buff.Length)) > 0 && !_shouldStop)
                {
                    stream.Write(buff, 0, toSend);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (br != null) br.Close();
            }

            return true;
        }

        /// <summary>
        /// Proprietà che permette di leggere le impostazioni di sincronizzazione
        /// </summary>
        public Settings syncSettings
        {
            get { return settings; }
        }

        /// <summary>
        /// Proprietà che setta la terminazione dei Task in Esecuzione
        /// </summary>
        public bool shouldStop
        {
            set { _shouldStop = value; }
        }
    }
}
