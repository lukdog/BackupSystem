using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Data.SQLite;

namespace Progetto_Server
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
            set { this._user = value.ToLower(); }
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


        /// <summary>
        /// Metodo che si occupa di prelevare dal DB un utente, nel caso non esistesse lo crea e lo salva nel DB
        /// </summary>
        /// <param name="c">Connessione APERTA al DB</param>
        /// <param name="folder">Directory che l'utente vuole sincronizzare</param>
        /// <param name="user">Nome Utente</param>
        /// <param name="pwd">Password</param>
        /// <param name="settings">Settings prelevate</param>
        /// <returns>True se prelievo o salvataggio riuscito</returns>
        public static bool getSettingsDB(SQLiteConnection c, String folder, String user, String pwd, out Settings settings)
        {

            String sql = "SELECT * FROM utenti WHERE nome=@name";
            String DBf, DBp, DBu;
            settings = null;
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(sql, c);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@name", user);
                SQLiteDataReader res = cmd.ExecuteReader();

                if (!res.HasRows)
                {
                    int thID = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine("(" + thID + ") -> Nuovo Utente Creato: " + user);
                    Settings s = newSettingsDB(c, user, pwd, folder);
                    if (s == null) return false;

                    s.active = false;
                    settings = s;
                    
                    return true;
                }
                else
                {
                    res.Read();
                    DBf = (String)res["folder"];
                    DBu = (String)res["nome"];
                    DBp = (String)res["password"];
                }  
            }
            catch (Exception e)
            {
                int thID = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine("(" + thID + ")_ERRORE: Eccezione durante autenticazione utente {0}: {1}", user, e.Message);
                return false;
            }

            if (DBu != user) return false;
            if (DBp != pwd) return false;

            settings = new Settings(DBf, DBu, DBp, null, 0);
            return true;
            
        }

        /// <summary>
        /// Metodo privato che inserisce un nuovo record nel DB
        /// </summary>
        /// <param name="c">Connessione APERTA al DB</param>
        /// <param name="user">Nome Utente</param>
        /// <param name="pwd">Password</param>
        /// <param name="folder">Directory da Sincronizzare</param>
        /// <returns></returns>
        private static Settings newSettingsDB(SQLiteConnection c, String user, String pwd, String folder)
        {
            String sql = "INSERT INTO UTENTI VALUES(@name, @pwd, @dir)";
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(sql, c);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@name", user.ToLower());
                cmd.Parameters.AddWithValue("@pwd", pwd);
                cmd.Parameters.AddWithValue("@dir", folder);
                if (cmd.ExecuteNonQuery() != 1)
                {
                    int thID = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine("(" + thID + ")_ERRORE: impossibile eseguire query: {0}", sql);
                    return null;
                }
            }
            catch (Exception e)
            {
                int thID = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine("(" + thID + ")_ERRORE: Eccezione durante inserimento utente {0} con query ({1}): {2}", user, sql, e.Message);
                return null;
            }

            return new Settings(folder, user, pwd, null, 0);

        }

        /// <summary>
        /// Metodo che si occupa di Eliminare un Record dal DB degli utenti
        /// </summary>
        /// <param name="c">Connessione Aperta al DB</param>
        /// <param name="s">Settings da Eliminare</param>
        /// <returns></returns>
        public static bool delSettingsDB(SQLiteConnection c, Settings s)
        {
            String sql = "DELETE FROM utenti WHERE nome=@name";
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(sql, c);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@name", s.user);
                cmd.ExecuteNonQuery();
            }
            catch
            {
                return false;
            }
            
            return true;
        }
    }
}
