using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Progetto_Client
{
    /// <summary>
    /// Classe che Rappresenta una Versione della Directory Sincronizzata,
    /// contiene l'elenco dei files che si trovano nella directory al momento della sincronizzazione
    /// </summary>
    [Serializable]
    [XmlType("Version")]
    public class Version
    {
        private String _date;
        private String _path;
        private int _nFiles;
        private bool _isDirectory;

        /// <summary>
        /// Costruttore della classe
        /// </summary>
        /// <param name="p">Path a cui la versione fa riferimento</param>
        public Version(String p)
        {
            _path = p;
            _nFiles = 0;
        }

        /// <summary>
        /// Costruttore della Classe
        /// </summary>
        /// <param name="p">path a cui la versione fa riferimento</param>
        /// <param name="d">Specifica se il path è relativo ad una directory</param>
        public Version(String p, bool d)
        {
            _path = p;
            _isDirectory = d;
        }

        public Version() { }

        /// <summary>
        /// Proprietà che memorizza la Data e Ora di Creazione della versione
        /// </summary>
        [XmlAttribute("time")]
        public String date
        {
            get { return _date; }
            set { _date = value; }
        }

        /// <summary>
        /// Proprietà che memorizza il path relativo alla versione
        /// </summary>
        [XmlAttribute("path")]
        public String path
        {
            get { return _path; }
            set { _path = value; }
        }

        /// <summary>
        /// Proprietà che memorizza se questa versione è relativa ad una Directory
        /// </summary>
        [XmlAttribute("isDir")]
        public bool isDirectory
        {
            get { return _isDirectory; }
            set { _isDirectory = value; }
        }

        /// <summary>
        /// Proprietà che memorizza se necessario la quantità di file da ripristinare per questa versione
        /// </summary>
         [XmlAttribute("nFiles")]
        public int numberOfFiles
        {
            set { _nFiles = value; }
            get { return _nFiles; }
        }

        /// <summary>
        /// Metodo che permette la stampa su schermo degli oggetti relativi a questa classe
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("Versione creata il {1}", _path, _date);
        }

        /// <summary>
        /// Metodo che si occupa della Serializzazione della versione su file di Testo
        /// </summary>
        /// <param name="s">Stream su cui effettuare la serializzazione</param>
        /// <returns>True se serializzazione completata con successo</returns>
        public bool serializeTo(Stream s)
        {
            XmlSerializer ser = new XmlSerializer(this.GetType());
            try
            {
                ser.Serialize(s, this);
            }
            catch
            {
                return false;
            }

            return true;

        }

        /// <summary>
        /// Metodo che si occupa della deserializzazione della Versione da File di Testo
        /// </summary>
        /// <param name="s">Stream da cui deserializzare</param>
        /// <returns>La Versione deserializzata</returns>
        public static Version deserializeFrom(Stream s)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Version));
            Version v = null;
            try
            {
                v = (Version)ser.Deserialize(s);
            }
            catch
            {
                return null;
            }

            return v;
        }

    }
}
