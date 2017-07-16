using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Xml.Serialization;

namespace Progetto_Client
{
    /// <summary>
    /// Classe che rappresenta i comandi scambiati tra Client e Server,
    /// può trasportare un comando, e un attributo, ad esempio: 
    /// cmd: NEW attribute: OBJ
    /// </summary>
    [Serializable]
    [XmlType("Command")]
    public class Command
    {
        /// <summary>
        /// Enumerazione type che rappresenta il tipo di comando che si può inviare
        /// (SYNC_REQ -> Richiesta di Sincronizzazione verso il server),
        /// (SYNC_RES -> Risposta di sincronizzazione),
        /// (LIST_REQ -> Richiesta dell'elenco file ripristinabili),
        /// (LIST_RES -> Risposta con elenco file),
        /// (NEW -> Nuovo file da memorizzare),
        /// (UPD -> File da Aggiornare),
        /// (DEL -> File da Eliminare),
        /// (VERS_REQ -> Richiesta delle Versioni per un determinato Path),
        /// (VERS_LIST -> Elenco delle Versioni disponibili),
        /// (REST_REQ -> Richiesta di Ripristino ad una determinata Versione),
        /// (SUCC -> Risposta Successo),
        /// (ERR -> Risposta Errore),
        /// (ERRPSW -> Password Errata),
        /// (END -> Fine Sincronizzazione)
        /// </summary>
        public enum type
        {
            SYNC_REQ, SYNC_RES, LIST_REQ, LIST_RES, NEW, UPD, DEL, VERS_REQ, VERS_LIST, REST_REQ, SUCC, ERR, END, ERRPSW
        };

        private type _cmd = type.ERR;
        private FileAttr _file = null;
        private Settings _settings = null;
        private ConcurrentDictionary<String, FileAttr> _dictionary = null;
        private List<dictElement<String, FileAttr>> proxyDic = null;
        private List<String> _filesPath = null;
        private List<Version> _versions = null;

        public Command() { }

        /// <summary>
        /// Costruttore per la clase Command, permette di settare il tipo di Comando da Inviare
        /// </summary>
        /// <param name="cmd">Comando da Inviare di tipo Command.type</param>
        public Command(type cmd)
        {
            this._cmd = cmd;
        }

        /// <summary>
        /// Propriatà cmd che permette di settare e prelevare il tipo di comando
        /// </summary>
        [XmlAttribute("cmd")]
        public type cmd
        {
            get { return _cmd; }
            set { _cmd = value; }
        }

        /// <summary>
        /// Proprietà file per trasportare le informazioni riguardanti un file nei comandi che lo richiedono:
        /// NEW, UPD, DEL
        /// Lancia un'Eccezione se si vuole aggiungere questo tipo di Oggetto ad un comando non valido
        /// </summary>
        //[XmlAttribute("file")]
        public FileAttr file
        {
            get { return _file; }
            set
            {
                _file = value;
            }
        }

        /// <summary>
        /// Proprietà settings per trasportare i parametri di sincronizzazione durante la SYNC_REQ
        /// Lancia un'eccezione se comando di altro tipo
        /// </summary>
        public Settings settings
        {
            get { return _settings; }
            set
            {
                _settings = value;
            }
        }

        /// <summary>
        /// Proprietà dictionary per trasportare l'elenco di file, solo per il comando SYNC_RES
        /// Lancia un'eccezione se comando di altro tipo.
        /// </summary>
        [XmlIgnore]
        public ConcurrentDictionary<String, FileAttr> dictionary
        {
            get { return _dictionary; }
            set
            {
                _dictionary = value;
            }
        }

        /// <summary>
        /// proprietà che permette di fare Set e Get del Dictionary sottoforma di lista
        /// In particolare è utile per la Serializzazione e la Deserializzazione della Classe Command
        /// </summary>
        [XmlArray("elements"), XmlArrayItem("element")]
        public List<dictElement<String, FileAttr>> elements
        {
            get
            {
                return proxyDic;
            }

            set
            {
                proxyDic = value;
            }

        }

        /// <summary>
        /// Proprietà che contiene una lista di Stringhe rappresentanti i path dei file presenti sul server
        /// </summary>
        [XmlArray("paths"), XmlArrayItem("path")]
        public List<String> filesPath
        {
            get { return _filesPath; }
            set { _filesPath = value; }
        }

        /// <summary>
        /// Proprietà che memorizza la versione di un file
        /// </summary>
        [XmlArray("versions"), XmlArrayItem("version")]
        public List<Version> versions
        {
            get { return _versions; }
            set { _versions = value; }
        }

        /// <summary>
        /// Metodo che Crea la Lista proxyDic per la Serializzazione del Dictionary
        /// </summary>
        private void buildList()
        {
            if (_dictionary == null)
                return;

            proxyDic = new List<dictElement<String, FileAttr>>();
            foreach (String k in _dictionary.Keys)
            {
                proxyDic.Add(new dictElement<String, FileAttr>(k, _dictionary[k]));
            }

        }

        /// <summary>
        /// Metodo che Crea il Dizionario dalla ProxyDic dopo la Deserializzazione
        /// </summary>
        private void buildDic()
        {

            if (proxyDic == null)
                return;

            _dictionary = new ConcurrentDictionary<String, FileAttr>();
            foreach (dictElement<String, FileAttr> item in proxyDic)
            {
                //_dictionary[item.key] = item.value;
                if (item == null)
                {
                    Console.WriteLine("Item = NULL");
                }
                _dictionary.TryAdd(item.key, item.value);
            }

            proxyDic = null;

        }

        /// <summary>
        /// Funzione per Serializzare un oggetto della classe Command su uno stream
        /// </summary>
        /// <param name="stream">Stream su cui scrivere la Serializzazione es: NetworkStream</param>
        /// <returns>True se Serializzazione Riuscita</returns>
        public bool serializeTo(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Command));
            MemoryStream ms = new MemoryStream();
            try
            {
                if (cmd == type.SYNC_RES)
                    buildList();

                ser.Serialize(ms, this);
                Byte[] len = System.BitConverter.GetBytes(ms.Length);
                stream.Write(len, 0, len.Length);
                ms.Position = 0;
                ms.CopyTo(stream);
            }
            catch (Exception)
            {
                //System.Windows.Forms.MessageBox.Show(e.Message);
                return false;
            }
            finally
            {
                if (proxyDic != null)
                    proxyDic = null;
            }

            return true;
        }

        /// <summary>
        /// Funzione Statica per la deserializzazione di un oggetto Command da uno Stream
        /// </summary>
        /// <param name="stream">Stream da cui deserializzare</param>
        /// <param name="tcp">TcpClient da cui deserializzare</param>
        /// <param name="exit">Riferimento allo shouldStop</param>
        /// <returns></returns>
        public static Command deserializeFrom(TcpClient tcp, Stream stream, ref bool exit)
        {
           
            XmlSerializer ser = new XmlSerializer(typeof(Command));
            Command c = null;
            MemoryStream ms = new MemoryStream();
            try
            {
                DateTime minute = DateTime.Now;
                int recvd=0, recvdd = 0;
                Byte[] sizeB = new Byte[8];
                int i = 0;
                while (recvdd != 8 && !exit)
                {
                    try
                    {
                        recvd = stream.Read(sizeB, recvdd, sizeB.Length);
                        if (recvd <= 0) return null;
                        recvdd += recvd;
                    }
                    catch(IOException)
                    {
                        if (!tcp.Connected) return null;
                        i++;
                        if (i >= 50) return null;
                    }             
                }

                if (exit) return null;

                long size = BitConverter.ToInt64(sizeB, 0);
                Byte[] buff = new Byte[1024];
                recvdd = 0;
                long diff = 0;
                i = 0;
                while (recvdd != size && !exit)
                {
                    try
                    {
                        if ((diff = size - recvdd) < 1024)
                        {
                            recvd = stream.Read(buff, 0, (int)diff);
                        }
                        else
                        {
                            recvd = stream.Read(buff, 0, 1024);
                        }

                        if(recvd == 0) return null;

                        ms.Write(buff, 0, recvd);
                        recvdd += recvd;
                    }
                    catch
                    {
                        if (!tcp.Connected) return null;
                        i++;
                        if (i >= 50) return null;
                    } 
                  
                }

                if (exit) return null;

                ms.Position = 0;

                c = (Command)ser.Deserialize(ms);

                if (c == null)
                {
                    Console.WriteLine("Deserializzazione non riuscita");
                }

                if (c.cmd == type.SYNC_RES)
                    c.buildDic();
            }
            catch (Exception e)
            {
                return null;
            }
            

            return c;

        }

    }
}
