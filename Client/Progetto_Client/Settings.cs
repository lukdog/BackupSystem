using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progetto_Client
{
    /// <summary>
    /// La Classe Settings si occupa di memorizzare le impostazioni per l'applicazione.
    /// </summary>
    public class Settings
    {
        private bool _active = false;
        private String _folder = null;
        private String _user = null;
        private UInt32 _port = 0;
        private String _server = null;
        private String _pwd = null;

        /// <summary>
        /// Costruttore di Default, utile per la Deserialization XML
        /// </summary>
        public Settings() { }

        /// <summary>
        /// Costruttore per la classe Settings, permette di creare un'istanza della classe con tutti i parametri impostati
        /// </summary>
        /// <param name="folder">Cartella che deve essere sincronizzata</param>
        /// <param name="user">Utente da Sincronizzare che utilizza l'applicazione</param>
        /// <param name="server">IP del server su cui effettuare la sincronizzazione</param>
        /// <param name="port">Porta TCP a cui collegarsi</param>
        public Settings(String folder, String user, String pwd, String server, UInt32 port)
        {
            this._active = true;
            this._folder = folder;
            this._user = user;
            this._server = server;
            this._port = port;
            this._pwd = pwd;
        }

        /// <summary>
        /// Proprietà che memorizza la Cartella da Sincronizzare con il server
        /// </summary>
        public String folder
        {
            get{return this._folder;}
            set{ this._folder = value;}
        }

        /// <summary>
        /// Proprietà che memorizza la password dell'utente
        /// </summary>
        public String pwd
        {
            get { return this._pwd; }
            set { this._pwd = value; }
        }

        /// <summary>
        /// Proprietà che memorizza l'utente che vuole effettuare la sincronizzazione
        /// </summary>
        public String user
        {
            get { return this._user; }
            set { this._user = value; }
        }

        /// <summary>
        /// Proprietà che memorizza l'IP del server su cui si effettua il Backup
        /// </summary>
        public String server
        {
            get { return this._server; }
            set { this._server = value; }
        }

        /// <summary>
        /// Proprietà che memorizza la Porta TCP del Server a cui collegarsi
        /// </summary>
        public UInt32 port
        {
            get { return this._port; }
            set { this._port = value; }
        }

        /// <summary>
        /// Proprietà che memorizza se queste impostazioni sono valide o no
        /// </summary>
        public bool active
        {
            get { return this._active; }
            set { this._active = value; }
        }

    }
}
