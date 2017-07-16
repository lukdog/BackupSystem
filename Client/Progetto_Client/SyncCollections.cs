using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progetto_Client
{
    /// <summary>
    /// Delegato per la scrittura di un messaggio, ad esempio per scrivere lo stato della sincronizzazione sulla main Window
    /// </summary>
    /// <param name="msg">Messaggio da Visualizzare</param>
    public delegate void msgWriter(String msg);

    /// <summary>
    /// Delegato per l'aggiornamento di un Valore, ad esempio per l'avanzamento di una progress bar
    /// </summary>
    /// <param name="upd"></param>
    public delegate void updValue(Double upd);

    /// <summary>
    /// Delegato per la scrittura di un'errore
    /// </summary>
    /// <param name="err">Errore da riportare</param>
    /// <param name="fatal">Indica se errore Fatale</param>
    public delegate void errWriter(String err, bool fatal);


    /// <summary>
    /// Classe che Contiene tutte le collezioni utili ai thread per lo scanning dei file e le impostazioni per l'Invio
    /// </summary>
    class SyncCollections
    {
        private ConcurrentDictionary<String, FileAttr> _serverFiles;
        private BlockingQueue<String> _tasks;
        private BlockingList<FileAttr> _newFiles;
        private BlockingList<FileAttr> _updFiles;
        private BlockingList<FileAttr> _delFiles;
        private Settings _settings;
        public int totFiles = 0;
        public event msgWriter writeMsg;
        public event errWriter writeErr;
        public event updValue updProg;

        /// <summary>
        /// Costruttore della classe, si preoccupa di istanziare le Collezioni utili.
        /// </summary>
        /// <param name="dic">Dizionario contenente le Informazioni riguardanti i file attualmente presenti sul server</param>
        /// <param name="set">Settings di Sincronizzazione</param>
        public SyncCollections( Settings set){
            _newFiles = new BlockingList<FileAttr>();
            _updFiles = new BlockingList<FileAttr>();
            _delFiles = new BlockingList<FileAttr>();
            _tasks = new BlockingQueue<String>();
            _settings = set;
        }

        /// <summary>
        /// Metodo che si occupa di inizializzare l'oggetto per una nuova sincronizzazione
        /// </summary>
        public void initCollection()
        {
            _newFiles = new BlockingList<FileAttr>();
            _updFiles = new BlockingList<FileAttr>();
            _delFiles = new BlockingList<FileAttr>();
            _tasks = new BlockingQueue<String>();
        }
        
        /// <summary>
        /// Proprietà che restituisce la collezione contenente i nuovi file da sincronizzare con il server
        /// </summary>
        public BlockingList<FileAttr> newFiles
        {
            get { return _newFiles; }
        }

        /// <summary>
        /// Proprietà che restituisce la collezione contenente i file da aggiornare sul server
        /// </summary>
        public BlockingList<FileAttr> updFiles
        {
            get { return _updFiles; }
        }

        /// <summary>
        /// Proprietà che restituisce la collezione dei file da eliminare dal server
        /// </summary>
        public BlockingList<FileAttr> delFiles
        {
            get { return _delFiles; }
        }

        /// <summary>
        /// Proprietà che restituisce il Dizionario con i file presenti sul server.
        /// </summary>
        public ConcurrentDictionary<String, FileAttr> serverFiles
        {
            get { return _serverFiles; }
            set { _serverFiles = value; }
        }

        /// <summary>
        /// Proprietà che restituisce la collezione dei tasks, cioè i file ancora da scansionare
        /// </summary>
        public BlockingQueue<String> tasks
        {
            get { return _tasks; }
        }

        /// <summary>
        /// Proprietà che restituisce le Impostazioni di Sincronizzazione
        /// </summary>
        public Settings settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// Metodo che permette di invocare il delegato per la scrittura di un messaggio
        /// </summary>
        /// <param name="msg">Messaggio da Stampare nello stato</param>
        public void sendMsg(String msg)
        {
            if (writeMsg != null)
            {
                writeMsg(msg);
            }
        }

        /// <summary>
        /// Metodo che permette di invocare il delegato per la scrittura di un errore Fatale
        /// </summary>
        /// <param name="err">Errore da Stampare</param>
        public void sendErrFatal(String err)
        {
            if (writeErr != null)
            {
                writeErr(err, true);
            }
        }

        /// <summary>
        /// Metodo che permette di invocare il delegato per la scrittura di un errore
        /// </summary>
        /// <param name="err">Errore da Stampare</param>
        public void sendErr(String err)
        {
            if (writeErr != null)
            {
                writeErr(err, false);
            }
        }

        /// <summary>
        /// Metodo che permette di invocare il delegato per l'update di un valore
        /// </summary>
        /// <param name="value">Nuovo Valore</param>
        public void updBar(Double value)
        {
            updProg(value);
        }

    }
}
