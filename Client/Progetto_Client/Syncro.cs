using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
    /// Classe che si occupa di effettuare la sincronizzazione con il server.
    /// Come prima cosa richiede al server l'elenco dei file, tramite il comando opportuno, in seguito tramite una serie di thread crea
    /// le collezioni di file da inviare al server e procede con l'invio.
    /// </summary>
    class Syncro
    {

        private SyncCollections _files = null;
        private Thread syncThread = null;
        private volatile bool _shouldStop = false;
        private const int nThreadCreate = 10;

        private const int readTimeout = 10000;
        private const int writeTimeout = 10000;
        private const int syncTimeout = 30000;

        private TcpClient sock = null;
        private NetworkStream netStream = null;

        /*
         * Valori Utili per l'incremento della Progress Bar
         * diffForFile -> percentuale da incrementare dopo l'invio di ogni file
         * prog -> valore attuale della progress Bar
         */
        Double diffForFile = 0;
        Double prog = 0;

        /// <summary>
        /// Costruttore per la classe Syncro
        /// </summary>
        /// <param name="s">Settings relative alla sincronizzazione da efferttuare</param>
        public Syncro(Settings s)
        {
            _files = new SyncCollections(s);
        }

        /// <summary>
        /// Proprietà per accedere al Thread Primario syncThread
        /// </summary>
        public Thread master
        {
            get { return syncThread; }
        }

        /// <summary>
        /// Proprietà per accedere al SyncCollections 
        /// </summary>
        public SyncCollections Files
        {
            get { return _files; }
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
        /// Metodo per Avviare la Sincronizzazione
        /// </summary>
        /// <returns>Ritorna True se Avvio riuscito. False altrimenti</returns>
        public bool startSync()
        {
            ThreadStart todo = new ThreadStart(primaryThread);
            try
            {
                syncThread = new Thread(todo);
                syncThread.Start();
            }
            catch (OutOfMemoryException)
            {
                _files.sendErrFatal("Impossibile Avviare la Sincronizzazione in questo momento, non ci sono abbastanza risorse di sistema diponibili");
                return false;
            }

            return true;

        }

        /// <summary>
        /// Metodo privato che mantiene attiva la sincronizzazione
        /// </summary>
        private void primaryThread()
        {
            while (!_shouldStop)
            {
                _files.updBar(0);
                syncTask();
                if (_shouldStop)
                    break;
                else
                {
                    int i = 0;
                    while (i < syncTimeout)
                    {
                        i += syncTimeout / 10;
                        Thread.Sleep(syncTimeout / 10);
                        if (_shouldStop) break;
                    }

                }
            }

            if (netStream != null)
                netStream.Close();

            if (sock != null)
                sock.Close();

        }

        /// <summary>
        /// Metodo che svolge i compiti per la sincronizzazione,
        /// Stabilisce la Connessione con Il server
        /// Effettua la scansione dei file crea le liste di sincronizzzazione e le serializza su netStream
        /// </summary>
        private void syncTask()
        {
            _files.initCollection();
            //Stabilisco connessione al Server
            _files.sendMsg("Stabilisco Connessione Con il Server...");
            if (!initConnection()) return;

            //Richiesta SYNC_REQ
            Command req = new Command(Command.type.SYNC_REQ);
            req.settings = _files.settings;
            if (!req.serializeTo(netStream))
            {
                _files.sendErrFatal("Impossibile Inviare la richiesta al server");
                _shouldStop = true;
                return;
            }

            //Ricevo SYNC_RES con il Dictionary
            Command res = Command.deserializeFrom(sock, netStream, ref _shouldStop);
            if (_shouldStop)
                return;
            else if (res == null)
            {
                _files.sendErrFatal("Nessuna Risposta Ricevuta dal Server");
                _shouldStop = true;
                return;
            }
            else if (res.cmd == Command.type.ERRPSW)
            {
                _files.sendErrFatal("La Password inserita è Errata, o lo stesso utente è attivo su più Client");
                _shouldStop = true;
                return;
            }
            else if (res.cmd != Command.type.SYNC_RES)
            {
                _files.sendErrFatal("Il Server ha Riscontrato un Errore");
                _shouldStop = true;
                return;
            }

            _files.serverFiles = res.dictionary;
            _files.updBar(10);

            //Scansiono la Directory indicata
            if (!scanFolder()) return;
            _files.updBar(25);

            //Preparo le Strutture da cui prelevare i File
            _files.sendMsg("Preparo per l'invio al Server...");
            List<FileAttr> news = _files.newFiles.getList();
            List<FileAttr> upd = _files.updFiles.getList();
            _files.totFiles = news.Count + upd.Count;
            List<FileAttr> dels = null;
            if (_files.serverFiles.Count != 0)
            {
                dels = new List<FileAttr>(_files.serverFiles.Values);
                _files.totFiles += dels.Count;
            }

            _files.updBar(30);

            //Se ci sono sincronizzazioni da fare
            if (_files.totFiles != 0)
            {
                diffForFile = 70.0 / _files.totFiles;
                prog = 30;
                //Inizio dell'invio e al Server
                _files.sendMsg("Inizio Sincronizzazione con il Server...");
                if (!sendFiles(news, Command.type.NEW)) return;
                if (!sendFiles(upd, Command.type.UPD)) return;
                if (dels != null)
                {
                    if (!sendFiles(dels, Command.type.DEL)) return;
                }

            }

            //Termino
            Command end = new Command(Command.type.END);
            if (!end.serializeTo(netStream))
            {
                _files.sendErrFatal("Impossibile Inviare Terminazione al Server");
                _shouldStop = true;
                return;
            }

            _files.sendMsg("Files Sincronizzati!");
            _files.updBar(100);
            netStream.Close();
            sock.Close();
        }

        /// <summary>
        /// Metodo Privato per stabilire la connessione con il server
        /// </summary>
        /// <returns>True se connessione stabilita correttamente</returns>
        private bool initConnection()
        {
            try
            {
                IPAddress server = IPAddress.Parse(_files.settings.server);
                IPEndPoint endP = new IPEndPoint(server, (Int32)_files.settings.port);
                sock = new TcpClient();
                sock.Connect(endP);
                netStream = sock.GetStream();
                netStream.ReadTimeout = readTimeout;
                netStream.WriteTimeout = writeTimeout;
            }
            catch (FormatException)
            {
                _files.sendErrFatal("Indirizzo IP specificato non Valido");
                _shouldStop = true;
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                _files.sendErrFatal("Indirizzo IP o porta specificati fuori dal Range Consentito");
                _shouldStop = true;
                return false;
            }
            catch (InvalidOperationException)
            {
                _files.sendErrFatal("Impossibile Connetersi al Server Specificato");
                _shouldStop = true;
                if (sock != null) sock.Close();
                return false;
            }
            catch (SocketException)
            {
                _files.sendErrFatal("Impossibile Connetersi al Server Specificato");
                _shouldStop = true;
                if (sock != null) sock.Close();
                return false;
            }
            catch (Exception e)
            {
                _files.sendErrFatal("Impossibile stabilire connessione: " + e.Message);
                _shouldStop = true;
                if (sock != null) sock.Close();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Metodo privato per la scansione della directory indicata
        /// </summary>
        /// <returns>True se scansione effettuata correttamente</returns>
        private bool scanFolder()
        {
            //Creazione dei Thread per la scansione
            _files.sendMsg("Preparo per la Scansione File...");
            Thread[] threadList = new Thread[nThreadCreate];
            ThreadStart toDo = new ThreadStart(scanningThread);
            try
            {
                for (int i = 0; i < nThreadCreate; i++)
                {
                    threadList[i] = new Thread(toDo);
                    threadList[i].Start();
                }
            }
            catch (OutOfMemoryException)
            {
                _files.sendErrFatal("Impossibile Avviare la Scansione dei file, non ci sono abbastanza risorse di sistema diponibili");
                _shouldStop = true;
                _files.tasks.closed = true;
                waitThreads(threadList);
                return false;
            }

            //Scansione della Cartella per la Creazione dei Tasks, utilizzo una coda FIFO in cui salvo le cartelle ancora da visionare
            _files.sendMsg("Scansiono i File...");
            try
            {
                Queue<DirectoryInfo> dirQueue = new Queue<DirectoryInfo>();
                dirQueue.Enqueue(new DirectoryInfo(_files.settings.folder));

                while (dirQueue.Count != 0 && !_shouldStop)
                {
                    DirectoryInfo dir = dirQueue.Dequeue();

                    foreach (var f in dir.GetFiles())
                    {
                        _files.tasks.enqueue(f.FullName);
                    }

                    foreach (var d in dir.GetDirectories())
                    {
                        dirQueue.Enqueue(d);
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                _files.sendErrFatal("Directory di Sincronizzazione scelta non trovata!");
                _shouldStop = true;
                return false;
            }
            catch (SecurityException)
            {
                _files.sendErrFatal("Non hai i permessi necessari per accedere a questa cartella!");
                _shouldStop = true;
                return false;
            }
            catch (ArgumentException)
            {
                _files.sendErrFatal("Il Percorso inserito contiene caratteri non validi");
                _shouldStop = true;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _files.sendErrFatal("Non hai il diritto di accesso alla Directory indicata o ad una sua sottocartella!");
                _shouldStop = true;
                return false;
            }
            catch (Exception e)
            {
                _files.sendErrFatal("A causa di un errore imprevisto è impossibile effettuare la scansione dei file in questo momento." + e.ToString());
                _shouldStop = true;
                return false;
            }
            finally
            {
                //Chiudo la Coda
                _files.tasks.closed = true;
                //Attendo che i thread terminino
                waitThreads(threadList);
            }

            //Se è stato chiesto di interrompersi, chiudo il Thread
            if (_shouldStop)
                return false;

            return true;
        }

        /// <summary>
        /// Metodo privato per l'Invio dei file appartenenti ad un tipo di lista
        /// </summary>
        /// <param name="l">Lista da cui prelevare i FileAttr</param>
        /// <param name="type">Tipo di Comando: NEW, UPD, DEL</param>
        /// <returns>True se file inviato correttamente</returns>
        private bool sendFiles(List<FileAttr> l, Command.type type)
        {
            foreach (FileAttr fa in l)
            {
                if (_shouldStop) return false;

                String state = "Sincronizzo: " + fa.path;
                if (state.Length > 124)
                {
                    state = state.Substring(0, 120);
                    state += "...";
                }
                _files.sendMsg(state);

                Command c = new Command(type);
                c.file = fa;
                if (!c.serializeTo(netStream))
                {
                    _files.sendErrFatal("Errore: Impossibile Comunicare correttamente con il Server");
                    _shouldStop = true;
                    return false;
                }

                if (type != Command.type.DEL)
                {
                    if (!sendFile(fa.path))
                    {
                        _files.sendErrFatal("Errore: Impossibile trasferire i file sul server");
                        _shouldStop = true;
                        return false;
                    }
                }

                //Aspetto comando risposta SUCC
                Command res = Command.deserializeFrom(sock, netStream, ref _shouldStop);
                if (_shouldStop)
                    return false;
                else if (res == null || res.cmd != Command.type.SUCC)
                {
                    _files.sendErrFatal("Errore: il Server indicato non risponde correttamente");
                    _shouldStop = true;
                    return false;
                }

                prog += diffForFile;
                _files.updBar(prog);
            }

            return true;
        }

        /// <summary>
        /// Metodo privato per l'invio di un file binario
        /// </summary>
        /// <param name="name">Nome del file</param>
        /// <returns>True se inviato correttamente</returns>
        private bool sendFile(String name)
        {
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(name, FileMode.Open));
                Byte[] buff = new Byte[1024];
                int toSend;
                while ((toSend = br.Read(buff, 0, buff.Length)) > 0 && !_shouldStop)
                {
                        netStream.Write(buff, 0, toSend);
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
        /// Metodo eseguito dai Thread che scansionano i file e creano le liste corrette
        /// </summary>
        private void scanningThread()
        {

            //Fino a quando non devo fermarmi
            while (!_shouldStop)
            {
                String path;
                Stream stream = null;

                //Se la Coda è bloccata e vuota ritorno
                if (!_files.tasks.dequeue(out path))
                    break;

                try
                {
                    //Prelevo le info del file
                    FileInfo f = new FileInfo(path);

                    //Calcolo il Checksum
                    stream = f.Open(FileMode.Open);
                    MD5 checksum = MD5.Create();
                    Byte[] ck = checksum.ComputeHash(stream);
                    FileAttr attr = null;

                    //Controllo se il file esiste già
                    if (_files.serverFiles.ContainsKey(path))
                    {
                        //Se è nei file del server, e se il checksum e diverso lo aggiungo ai file di cui fare update
                        if (_files.serverFiles.TryRemove(path, out attr))
                        {
                            if (!attr.checksum.Equals(Convert.ToBase64String(ck)))
                            {
                                _files.updFiles.add(new FileAttr(path, f.DirectoryName, Convert.ToBase64String(ck), f.Length));
                            }
                        }
                    }
                    else
                    {
                        //File non presente sul Server, lo aggiungo ai nuovi
                        _files.newFiles.add(new FileAttr(path, f.DirectoryName, Convert.ToBase64String(ck), f.Length));
                    }
                }
                catch (SecurityException)
                {
                    _files.sendErr("Non hai i permessi necessari per accedere al file " + path);
                    FileAttr rem;
                    _files.serverFiles.TryRemove(path, out rem);
                }
                catch (UnauthorizedAccessException)
                {
                    _files.sendErr("Accesso al file " + path + " Negato!");
                    FileAttr rem;
                    _files.serverFiles.TryRemove(path, out rem);
                }
                catch (IOException)
                {
                    //_files.sendErr("Impossibile Sincronizzare il file " + path + " perchè in uso in questo momento!");
                    FileAttr rem;
                    _files.serverFiles.TryRemove(path, out rem);
                }
                catch (Exception e)
                {
                    _files.sendErrFatal("A causa di un errore imprevisto è impossibile effettuare la scansione dei file in questo momento." + e.ToString());
                    _shouldStop = true;
                    return;
                }
                finally
                {
                    if (stream != null) { stream.Close(); }
                }

            }
        }

        /// <summary>
        /// Metodo che si occupa di Aspettare la terminazione dei threads
        /// </summary>
        /// <param name="list">Array di Thread da Attendere</param>
        public void waitThreads(Thread[] list)
        {
            foreach (var t in list)
            {
                t.Join();
            }
        }

    }
}
