using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Progetto_Client
{
    /// <summary>
    /// Classe che si occupa di Generare una "Transazione" su una determinata Directory, 
    /// permette di fare commit o rollback per portare la directory allo stato originale.
    /// Se non viene effettuato il Commit o il Rollback verrà eseguito autoaticamente il Rollback
    /// </summary>
    class DirTransaction
    {
        private String baseFolder;
        private String tmpFolder;
        private bool ended = false;

        /// <summary>
        /// Costruttore della classe.
        /// </summary>
        /// <param name="folder">Stringa contenente il nome della Directory su cui avviare una transazione</param>
        public DirTransaction(String folder)
        {
            baseFolder = folder;
            tmpFolder = Path.GetTempPath();

            String tmp = tmpFolder + "1";
            DirectoryInfo d = new DirectoryInfo(tmp);
            int i = 2;
            while (d.Exists)
            {
                tmp = tmpFolder + i.ToString();
                d = new DirectoryInfo(tmp);
                i++;
            }

            d.Create();
            tmpFolder = d.FullName;
            copyFiles(new DirectoryInfo(baseFolder), new DirectoryInfo(tmpFolder));
        }

        /// <summary>
        /// Costruttore della Classe
        /// </summary>
        /// <param name="folder">Oggetto DirectoryInfo relativo alla Directory su cui avviare una transazione</param>
        public DirTransaction(DirectoryInfo folder)
        {
            baseFolder = folder.FullName;
            tmpFolder = Path.GetTempPath();

            String tmp = tmpFolder + "1";
            DirectoryInfo d = new DirectoryInfo(tmp);
            int i = 2;
            while (d.Exists)
            {
                tmp = tmpFolder + i.ToString();
                d = new DirectoryInfo(tmp);
                i++;
            }
            d.Create();
            tmpFolder = d.FullName;
            copyFiles(new DirectoryInfo(baseFolder), new DirectoryInfo(tmpFolder));
        }

        /// <summary>
        /// Permette di fare il Commit
        /// </summary>
        public void commit()
        {
            if (ended) return;
            ended = true;
            DirectoryInfo d = new DirectoryInfo(tmpFolder);
            d.Delete(true);
        }

        /// <summary>
        /// Effettua il Rollback
        /// </summary>
        public void rollback()
        {
            if (ended) return;
            ended = true;
            DirectoryInfo d = new DirectoryInfo(baseFolder);
            cleanDir(d);
            copyFiles(new DirectoryInfo(tmpFolder), new DirectoryInfo(baseFolder));
            DirectoryInfo del = new DirectoryInfo(tmpFolder);
            del.Delete(true);
        }

        /// <summary>
        /// Proprietà che memorizza la Directory su cui è attiva la transazione
        /// </summary>
        public String baseDirectory
        {
            get { return baseFolder; }
        }

        /// <summary>
        /// Proprietà che memorizza la directory dove è memorizzato il backup temporaneo
        /// </summary>
        public String tmpDirectory
        {
            get { return tmpFolder; }
        }

        /// <summary>
        /// Distruttore che effettua il rollback se ne commit ne rollback sono già stati chiamati
        /// </summary>
        ~DirTransaction()
        {
            if (!ended)
                this.rollback();
        }

        /// <summary>
        /// Metodo statico che effettua la copia dei files
        /// </summary>
        /// <param name="source">Directory sorgente</param>
        /// <param name="target">Directory Destinazione</param>
        public static void copyFiles(DirectoryInfo source, DirectoryInfo target)
        {

            if (!source.Exists)
                return;

            if (!target.Exists)
                Directory.CreateDirectory(target.FullName);

            // Copio i File nella nuova Directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            // Copio il contenuto delle subdirectory ricorsivamente
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                copyFiles(diSourceSubDir, nextTargetSubDir);
            }

        }

        /// <summary>
        /// Metodo Statico che si occupa di eliminare tutti i files contenuti in una cartella
        /// </summary>
        /// <param name="source">Directory da pulire</param>
        public static void cleanDir(DirectoryInfo source)
        {

            if (!source.Exists)
                return;

            try
            {
                // Elimino i file dalla Directory.
                foreach (FileInfo fi in source.GetFiles())
                {
                    fi.Delete();
                }

                // Elimino tutte le subdirectory
                foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
                {
                    cleanDir(diSourceSubDir);
                    diSourceSubDir.Delete(true);
                }
            }
            catch (Exception)
            {
                return;
            }

            

        }
    }
}
