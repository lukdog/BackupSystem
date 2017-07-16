using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;

namespace Progetto_Client
{
    /// <summary>
    /// Classe FileAttr, si occupa di memorizzare le informazioni riguardanti un file.
    /// </summary>
    [Serializable]
    [XmlType("FileAttr")]
    public class FileAttr
    {
        private String _path = null;
        private String _directory = null;
        private String _checksum = null;
        private long _size = 0;

        /// <summary>
        /// Costruttore per la classe FileAttr
        /// </summary>
        /// <param name="path">Percorso del file, nome incluso</param>
        /// <param name="dir">Directory a cui appartiene il File</param>
        /// <param name="checksum">Checksum calcolato del file</param>
        /// <param name="size">Dimensione in Bytes del file</param>
        public FileAttr(String path, String dir, String checksum, long size)
        {
            this._path = path;
            this._checksum = checksum;
            this._size = size;
            this._directory = dir;
        }

        public FileAttr() { }

        /// <summary>
        /// Path del File, con nome file compreso
        /// </summary>
        [XmlAttribute("path")]
        public String path
        {
            get { return _path; }
            set { _path = value; }
        }

        /// <summary>
        /// Directory a cui appartiene il file
        /// </summary>
        [XmlAttribute("directory")]
        public String directory
        {
            get { return _directory; }
            set { _directory = value; }
        }

        /// <summary>
        /// Checksum MD5 del file
        /// </summary>
        [XmlAttribute("checksum")]
        public String checksum
        {
            get { return _checksum; }
            set { _checksum = value; }
        }

        /// <summary>
        /// Dimensione in Byte del file
        /// </summary>
        [XmlAttribute("size")]
        public long size
        {
            get { return _size; }
            set { _size = value; }
        }

        /// <summary>
        /// Metodo per la stampa dell'oggetto
        /// </summary>
        /// <returns>Stringa Formattata</returns>
        public override string ToString()
        {
            return "Nome: " + path + " Checksum: " + checksum + " Size: " + size;
        }


        /// <summary>
        /// Funzione che serializza i dati su uno Stream
        /// </summary>
        /// <param name="stream">Stream su cui serializzare i dati, esempio un NetworkStream</param>
        /// <returns></returns>
        public bool serializeTo(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(FileAttr));
            try
            {
                ser.Serialize(stream, this);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }


        /// <summary>
        /// Funzione Statica per la deserializzazione di un oggetto FileAttr da uno Stream
        /// </summary>
        /// <param name="stream">Stream da cui deserializzare</param>
        /// <returns></returns>
        public static FileAttr deserializeFrom(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(FileAttr));
            FileAttr f = null;

            try
            {
                f = (FileAttr)ser.Deserialize(stream);
            }
            catch (Exception)
            {
                return null;
            }

            return f;

        } 

    }
}
