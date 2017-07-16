using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.IO;
using System.Windows.Media.Animation;

namespace Progetto_Client
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Settings applicationSettings = null;
        private Syncro syncObj = null;
        private Restore restObj = null;
        private volatile Boolean closing = false;

        /// <summary>
        /// Costruttore per la MainWindow, si occupa di inizializzare l'app
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Recupero impostazioni se già salvate
            try
            {
                applicationSettings = Functions.getSettings();
                if (applicationSettings != null && applicationSettings.active)
                {
                    settingsServerBox.Text = applicationSettings.server;
                    settingsFolderBox.Text = applicationSettings.folder;
                    settingsUserBox.Text = applicationSettings.user;
                    settingsPwdBox.Password = applicationSettings.pwd;
                    settingsPortBox.Text = System.Convert.ToString(applicationSettings.port);
                }
            }
            catch (Exception)
            {
                System.Windows.Forms.MessageBox.Show("Prima di Effettuare la Sincronizzazione è necessario settare le impostazioni dal tab Opzioni.", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
            }


        }

        /// <summary>
        /// Handler per il click del bottone per salvare le impostazioni, 
        /// prova a salvare le impostazioni settate, 
        /// se non riesce visualizza una MsgBox
        /// </summary>
        private void saveSettings(object sender, RoutedEventArgs e)
        {
            String server = settingsServerBox.Text;
            String folder = settingsFolderBox.Text;
            String user = settingsUserBox.Text;
            String pwd = settingsPwdBox.Password;
            UInt32 port = 0;

            //Controllo se vuoti
            if (user.Equals("") || folder.Equals("") || server.Equals("") || pwd.Equals(""))
            {
                System.Windows.Forms.MessageBox.Show("Tutti i Campi devono essere settati", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            //Controllo se porta valida
            try
            {
                port = System.Convert.ToUInt32(settingsPortBox.Text);
            }
            catch (Exception)
            {
                applicationSettings.active = false;
                System.Windows.Forms.MessageBox.Show("Valore di porta inserito non valido.", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                return;
            }

            //Controllo se IP valido
            try
            {
                IPAddress s = IPAddress.Parse(server);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Indirizzo del Server non valido!", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            //Controllo se Directory Valida
            try
            {
                DirectoryInfo d = new DirectoryInfo(folder);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Percorso inserito non valido!", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            //Controllo se Utente valido
            if(!Functions.isUserValid(user, System.IO.Path.GetInvalidFileNameChars()))
            {
                System.Windows.Forms.MessageBox.Show("L'utente inserito contiene caratteri non validi!", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            //Controllo Lunghezze
            if (user.Length > 20 || pwd.Length > 30)
            {
                System.Windows.Forms.MessageBox.Show("Utente o Password troppo lunghi, possono avere un massio di 20(User) e 20(password) caratteri", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            applicationSettings = new Settings(folder, user, pwd, server, port);

            try
            {
                Functions.saveSettings(applicationSettings);
            }
            catch (Exception)
            {
                applicationSettings.active = false;
                System.Windows.Forms.MessageBox.Show("Impossibile Salvare le impostazioni in Questo Momento.", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                return;
            }
            System.Windows.Forms.MessageBox.Show("Impostazioni Salvate con Successo.", "Successo", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);

        }

        /// <summary>
        /// Handler per il Click sul bottone folderSelect, 
        /// ha il compito di far selezionare all'utente la cartella da sincronizzare 
        /// e scrivere il percorso nella apposita textBox
        /// </summary>
        private void selectFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult ctrl = dialog.ShowDialog();

            if (ctrl == System.Windows.Forms.DialogResult.OK)
            {
                settingsFolderBox.Text = dialog.SelectedPath;
            }
            else
            {
                settingsFolderBox.Text = "";
            }

        }

        /// <summary>
        /// Handler per il Click sul bottone btnStart per avviare la sincronizzazione
        /// </summary>
        private void startSync(object sender, RoutedEventArgs e)
        {
            //Controllo siano state settate le impostazioni
            if (applicationSettings == null || !applicationSettings.active)
            {
                System.Windows.Forms.MessageBox.Show("Prima di Avviare la Sincronizzazione è necessario Settare i parametri dal tab Opzioni");
                return;
            }

            closing = false;

            //Instanzio la classe di sincronizzazione e la avvio
            syncObj = new Syncro(applicationSettings);
            syncObj.Files.writeErr += errorHandler;
            syncObj.Files.updProg += updProgBar;
            syncObj.Files.writeMsg += stateWriter;
            syncObj.startSync();

            //Rendo Visibile il bottone di Stop
            btnStop.Visibility = System.Windows.Visibility.Visible;
            btnStart.Visibility = System.Windows.Visibility.Hidden;
            tabSetting.IsEnabled = false;
            tabVersion.IsEnabled = false;

            //Azzero la progressBar e il messaggio di stato
            updProgBar(0);

        }

        /// <summary>
        /// Hanler per il Click sul bottone btnStop
        /// </summary>
        private void stopSync(object sender, RoutedEventArgs e)
        {

            if (closing) return;
            closing = true;
            //Elimino il Delegato per l'aggiornamento dello Stato
            syncObj.Files.writeMsg -= stateWriter;
            syncObj.Files.updProg -= updProgBar;
            stateWriter("Attendo Chiusura...");

            //Attendo chiusura del Thread Principale
            if (syncObj != null && syncObj.master != null)
            {
                syncObj.shouldStop = true;
                syncObj.master.Join();
            }
        
            //Reimposto la Schermata per Sincronizzazione non attiva
            stateWriter("Sincronizzazione non Attiva");
            btnStart.Visibility = System.Windows.Visibility.Visible;
            btnStop.Visibility = System.Windows.Visibility.Hidden;
            updProgBar(0);
            tabSetting.IsEnabled = true;
            tabVersion.IsEnabled = true;
        }

        /// <summary>
        /// Metodo per settare il Valore della progressBar di Sincronizzazione ad esempio da aggiungere ad un evento
        /// </summary>
        /// <param name="v">Valore a cui deve essere settata la progressBar</param>
        public void updProgBar(Double v)
        {
            syncProg.Dispatcher.Invoke(new Action(() =>
            {
                if (v > 100)
                    syncProg.Value = 100;
                syncProg.Value = v;
            }));
        }

        /// <summary>
        /// Metodo per settare il Valore della progressBar di Restoring ad esempio da aggiungere ad un evento
        /// </summary>
        /// <param name="v">Valore a cui deve essere settata la progressBar</param>
        public void updRestProgBar(Double v)
        {
            restProg.Dispatcher.Invoke(new Action(() =>
            {
                if (v > 100)
                    restProg.Value = 100;
                restProg.Value = v;
            }));
        }

        /// <summary>
        /// Metodo per attivare una MessageBox di errore, ad esempio tramite un delegato, 
        /// si occupa anche di impostare la schermata in Sincronizzazione Spenta
        /// </summary>
        /// <param name="msg">Messaggio da stampare nella MessageBox</param>
        /// <param name="fatal">Indica se errore comporta Terminazione Sincronizzazione</param>
        public void errorHandler(String msg, bool fatal)
        {

            if (fatal && !closing)
            {
                closing = true;
                System.Windows.Forms.MessageBox.Show(msg, "Errore Fatale", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                stateWriter("Sincronizzazione non Attiva");
                updProgBar(0);

                btnStop.Dispatcher.Invoke(new Action(() => { btnStop.Visibility = System.Windows.Visibility.Hidden; }));
                btnStart.Dispatcher.Invoke(new Action(() => { btnStart.Visibility = System.Windows.Visibility.Visible; }));
                tabSetting.Dispatcher.Invoke(new Action(() => { tabSetting.IsEnabled = true; }));
                tabVersion.Dispatcher.Invoke(new Action(() => { tabVersion.IsEnabled = true; }));
            }
            else
                System.Windows.Forms.MessageBox.Show(msg, "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);

            return;
        }

        /// <summary>
        /// Metodo per attivare una MessageBox di errore, ad esempio tramite un delegato, 
        /// si occupa anche di impostare la schermata in Sincronizzazione Spenta
        /// </summary>
        /// <param name="msg">Messaggio da stampare nella MessageBox</param>
        /// <param name="fatal">Indica se errore comporta Terminazione Sincronizzazione</param>
        public void versionErrorHandler(String msg, bool fatal)
        {
            if (fatal && !closing)
            {
                closing = true;
                System.Windows.Forms.MessageBox.Show(msg, "Errore Fatale", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                tabSync.Dispatcher.Invoke(new Action(() => { tabSync.IsSelected = true; tabSync.IsEnabled = true; }));
                tabSetting.Dispatcher.Invoke(new Action(() => { tabSetting.IsEnabled = true; }));
                tabVersion.Dispatcher.Invoke(new Action(() => { tabVersion.IsEnabled = true; }));
                versionPanel.Dispatcher.Invoke(new Action(() => { versionPanel.Visibility = System.Windows.Visibility.Visible; }));
            }
            else
                System.Windows.Forms.MessageBox.Show(msg, "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);

            return;
        }

        /// <summary>
        /// Metodo per Scrivere lo stato della sincronizzazione nella textBlock dello stato della sincronizzazione
        /// Chiamabile da Delegato
        /// </summary>
        /// <param name="msg">Messaggio da Stampare</param>
        private void stateWriter(String msg)
        {
            if (stateBox.Dispatcher.CheckAccess())
            {
                stateBox.Text = msg;
            }
            else
            {
                stateBox.Dispatcher.Invoke(new Action(() => { stateBox.Text = msg; }));
            }
        }

        /// <summary>
        /// Metodo per Scrivere lo stato della sincronizzazione nella textBlock per lo stato del restoring
        /// Chiamabile da Delegato
        /// </summary>
        /// <param name="msg">Messaggio da Stampare</param>
        private void stateRestoringWriter(String msg)
        {
            if (stateBox.Dispatcher.CheckAccess())
            {
                stateRestBox.Text = msg;
            }
            else
            {
                stateRestBox.Dispatcher.Invoke(new Action(() => { stateRestBox.Text = msg; }));
            }
        }

        /// <summary>
        /// Metodo che si occupa di avviare il retrieving dei file dal server quando si apre il pannello Restore
        /// </summary>
        private void refreshFilesList(object sender, MouseButtonEventArgs e)
        {
            refreshFileListFunc();
        }

        /// <summary>
        /// Metodo che si occupa di avviare il retrieving dei file dal server quando si clicca sul tasto Aggiorna
        /// </summary>
        private void refreshFilesList(object sender, RoutedEventArgs e)
        {
            refreshFileListFunc();
        }

        /// <summary>
        /// Metodo che si occupa di avviare la richiesta per l'elenco dei file sul server
        /// </summary>
        private void refreshFileListFunc()
        {
            //Controllo siano state settate le impostazioni
            if (applicationSettings == null || !applicationSettings.active)
            {
                System.Windows.Forms.MessageBox.Show("Prima di Avviare la Sincronizzazione è necessario Settare i parametri dal tab Opzioni");
                tabSetting.IsSelected = true;
                return;
            }

            closing = false;

           
            //Disabilito i Pulsanti e gli altri elementi
            btnRefresh.IsEnabled = false;
            btnRestore.IsEnabled = false;
            versionTree.Visibility = System.Windows.Visibility.Hidden;
            versionWaiting.Visibility = System.Windows.Visibility.Visible;

            //Disabilito gli Altri tab e Panel
            tabSetting.IsEnabled = false;
            tabSync.IsEnabled = false;
            versionListPanel.Visibility = System.Windows.Visibility.Hidden;
            restoringPanel.Visibility = System.Windows.Visibility.Hidden;
            versionPanel.Visibility = System.Windows.Visibility.Visible;

            restObj = new Restore(applicationSettings);
            restObj.writeErr += versionErrorHandler;
            restObj.populateTree += Functions.PopulateTreeView;
            restObj.populatedTree += enableRestore;
            restObj.retrieveFiles(versionTree);

        }

        /// <summary>
        /// Metodo Handler del click sul pulsante che annulla il recupero delle versioni
        /// </summary>
        private void exitRestore(object sender, RoutedEventArgs e)
        {
            if (closing) return;
            closing = true;

            if (restObj != null)
            {
                restObj.shouldStop = true;
                if (restObj.master != null)
                    restObj.master.Join();
            }
            
            tabSync.IsSelected = true;
            tabSync.IsEnabled = true;
            tabSetting.IsEnabled = true;
            tabVersion.IsEnabled = true;
            versionPanel.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// Metodo che abilita i pulsanti per il Restore
        /// </summary>
        private void enableRestore()
        {
            if (btnRefresh.Dispatcher.CheckAccess())
                btnRefresh.IsEnabled = true;
            else
                btnRefresh.Dispatcher.Invoke(new Action(() => { btnRefresh.IsEnabled = true; }));

            if (btnRestore.Dispatcher.CheckAccess())
                btnRestore.IsEnabled = true;
            else
                btnRestore.Dispatcher.Invoke(new Action(() => { btnRestore.IsEnabled = true; }));
        }

        /// <summary>
        /// Metodo che si occupa di avviare la richiesta versioni
        /// </summary>
        private void aksForVersions(object sender, RoutedEventArgs e)
        {
            if (restObj == null)
            {
                tabSync.IsSelected = true;
                tabSync.IsEnabled = true;
                tabSetting.IsEnabled = true;
                return;
            }
            else
            {
                restObj.master.Join();
            }

            //Disabilito i Pulsanti e gli altri elementi
            btnRestoreVersion.IsEnabled = false;
            versionPanel.Visibility = System.Windows.Visibility.Hidden;
            versionList.Visibility = System.Windows.Visibility.Hidden;
            restoringPanel.Visibility = System.Windows.Visibility.Hidden;
            versionListPanel.Visibility = System.Windows.Visibility.Visible;
            versionWaitingList.Visibility = System.Windows.Visibility.Visible;
            
            restObj = new Restore(applicationSettings);
            restObj.writeErr += versionErrorHandler;
            restObj.populateList += Functions.PopulateListBox;
            restObj.populatedList += enableRestoreVersion;

            //Ricavo path dell'elemento di cui si vuole effettuare il ripristino
            String pathToRestore;
            bool isDir = false;
            if (versionTree.SelectedItem != null)
            {
                pathToRestore = applicationSettings.folder + @"\";
                pathToRestore += ((String)((TreeViewItem)versionTree.SelectedItem).Tag);
                if (((TreeViewItem)versionTree.SelectedItem).HasItems)
                {
                    pathToRestore += @"\";
                    isDir = true;
                }
            }
            else
            {
                pathToRestore = applicationSettings.folder + @"\";
                isDir = true;
            }

            //Eseguo Richiesta al Server e popolo la lista
            restObj.retrieveVersions(versionList, new Version(pathToRestore, isDir));


        }

        /// <summary>
        /// Metodo che abilita i pulsanti per il Restore
        /// </summary>
        private void enableRestoreVersion()
        {
            if (btnRestoreVersion.Dispatcher.CheckAccess())
                btnRestoreVersion.IsEnabled = true;
            else
                btnRestoreVersion.Dispatcher.Invoke(new Action(() => { btnRestoreVersion.IsEnabled = true; }));
        }

        /// <summary>
        /// Metodo che si occupa di avviare il ripristino di una versione
        /// </summary>
        private void restoreVersion(object sender, RoutedEventArgs e)
        {

            if (restObj == null)
            {
                tabSync.IsSelected = true;
                tabSync.IsEnabled = true;
                tabSetting.IsEnabled = true;
                return;
            }
            else
            {
                restObj.master.Join();
            }

            
            if (versionList.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("Per Ripristinare devi scegliere la versione dalla lista.", "Errore", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                return;
            }
            Version toRestore = (Version)((ListBoxItem)versionList.SelectedItem).Tag;

            restProg.Value = 0;
            stateRestBox.Text = "Avvio Ripristino dei Files";

            restoringPanel.Visibility = System.Windows.Visibility.Visible;
            versionListPanel.Visibility = System.Windows.Visibility.Hidden;
            tabVersion.IsEnabled = false;

            /*Istanzio l'oggetto per il Restore*/
            restObj = new Restore(applicationSettings);
            restObj.writeErr += versionErrorHandler;
            restObj.restored += finishRestore;
            restObj.stateWrite += stateRestoringWriter;
            restObj.updProg += updRestProgBar;
            restObj.retrieveVersion(toRestore);
        }

        /// <summary>
        /// Metodo eseguito alla fine del ripristino
        /// </summary>
        private void finishRestore()
        {
            if (closing) return;

            System.Windows.Forms.MessageBox.Show("Ripristino dei File effettuato con successo.", "Successo", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
       
            tabSync.Dispatcher.Invoke(new Action(() => { tabSync.IsSelected = true; tabSync.IsEnabled = true; }));
            tabSetting.Dispatcher.Invoke(new Action(() => { tabSetting.IsEnabled = true; }));
            tabVersion.Dispatcher.Invoke(new Action(() => { tabVersion.IsEnabled = true; }));
            versionPanel.Dispatcher.Invoke(new Action(() => { versionPanel.Visibility = System.Windows.Visibility.Visible; }));

        }

        /// <summary>
        /// Metodo Eseguito Alla chiusura dell'applicazione
        /// </summary>
        private void closeEvent(object sender, System.ComponentModel.CancelEventArgs e)
        {
         
            if (syncObj != null)
            {
                if(syncObj.master != null)
                    if(syncObj.master.IsAlive)
                    {
                        System.Windows.Forms.MessageBox.Show("Attendere la Terminazione delle Operazioni o Terminarle manualmente");
                        e.Cancel = true;
                    }
                        
            }


            if (restObj != null)
            {
                if(restObj.master != null)
                    if (restObj.master.IsAlive)
                    {
                        System.Windows.Forms.MessageBox.Show("Attendere la Terminazione delle Operazioni o Terminarle manualmente");
                        e.Cancel = true;
                    }
            }


        }

    }
}
