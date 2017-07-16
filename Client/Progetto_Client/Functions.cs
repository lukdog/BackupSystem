using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;

namespace Progetto_Client
{
    /// <summary>
    /// Classe Statica che contiene una serie di funzioni utili per l'applicazione
    /// </summary>
    static class Functions
    {
        /// <summary>
        /// Si Occupa di prelevare le Impostazioni dal file settings.sgs ritorna 
        /// un Oggetto Settings se ci riesce, altrimenti lancia un'eccezione
        /// </summary>
        /// <returns>Oggetto Settings con le impostazioni contenute nel file settings.sgs</returns>
        /// <exception cref="Exception">Messaggio da visualizzare in textBox</exception>
       static public Settings getSettings(){
           FileStream f = null;
           Settings i = null;

           try
           {
               f = File.Open("settings.sgs", FileMode.Open);
               XmlSerializer ser = new XmlSerializer(typeof(Settings));
               i = (Settings) ser.Deserialize(f);
           }
           catch (Exception)
           {
               throw new Exception("Necessario Impostare i Parametri di Sincronizzazione dal tab Opzioni!");
           }
           finally
           {
               if (f != null) f.Close();
           }

           return i;
       }

        /// <summary>
        /// Funzione che si occupa di Salvare tramite serializzazione le impostazioni sul file settings.sgs
        /// </summary>
        /// <param name="i">Oggetto Settings da Serializzare</param>
       static public void saveSettings(Settings i)
       {
           FileStream f = null;
 
           if (i == null || !i.active)
               throw new Exception("Impostazioni Invalide");

           try
           {
               f = File.Open("settings.sgs", FileMode.Create);
               XmlSerializer ser = new XmlSerializer(typeof(Settings));
               ser.Serialize(f, i);
           }
           catch(Exception)
           {
               throw new Exception("Impossibile salvare le Impostazioni al momento!" );
           }
           finally
           {
               if (f != null) f.Close();
           }
       }

       /// <summary>
       /// Metodo che Controlla se l'username è un valore valido
       /// </summary>
       /// <param name="user">User Passato come Parametro</param>
       /// <param name="invalidChars">Array contenente i caratteri non validi</param>
       /// <returns>True se Valido False se non</returns>
       static public bool isUserValid(String user, char[] invalidChars)
       {
           return user.IndexOfAny(invalidChars) == -1;
       }

       /// <summary>
       /// Metodo Statico che si occupa di popolare una TreeView a partire da una Collezione di Path
       /// </summary>
       /// <param name="tree">TreeView da popolare</param>
       /// <param name="paths">Collezione di Paths</param>
       /// <param name="pathSeparator">Carattere Separatore path es '\\'</param>
       /// <returns>True se Popolato Correttamente</returns>
       static public void PopulateTreeView(TreeView tree, IEnumerable<String> paths, char pathSeparator)
       {
           tree.Items.Clear();
           TreeViewItem last = null;
           String subPathAgg;
           foreach (String path in paths)
           {
               subPathAgg = String.Empty;
               foreach (String subPath in path.Split(pathSeparator))
               {
                   TreeViewItem node = null;
                   if (last == null)
                       node = findTreeItem(subPath, tree.Items);
                   else
                       node = findTreeItem(subPath, last.Items);

                   if (node == null)
                   {
                       if (last == null)
                       {
                           subPathAgg += subPath;
                           int index = tree.Items.Add(new TreeViewItem() { Header = subPath, Tag = subPathAgg });
                           if (index < 0)
                               return;
                           else
                               last = (TreeViewItem)tree.Items[index];
                       }
                       else
                       {
                           subPathAgg += pathSeparator + subPath;
                           int index = last.Items.Add(new TreeViewItem() { Header = subPath, Tag = subPathAgg });
                           if (index < 0)
                               return;
                           else
                               last = (TreeViewItem)last.Items[index];
                       }
                   }
                   else
                   {
                       subPathAgg = (String)node.Tag;
                       last = node;
                   }
               }
               last = null;
           }

           tree.Visibility = System.Windows.Visibility.Visible;
           return;

       }

       /// <summary>
       /// Metodo Statico che si occupa di cercare un Item in una collezione
       /// </summary>
       /// <param name="header">Header per il Confronto</param>
       /// <param name="items">Collezione in cui cercare</param>
       /// <returns>Item Cercato o Null se non trovato</returns>
       static public TreeViewItem findTreeItem(String header, ItemCollection items)
       {
           if (items.Count == 0)
               return null;

           foreach (TreeViewItem item in items)
           {
               if (header.Equals((String)item.Header))
               {
                   return item;
               }
           }

           return null;

       }

        /// <summary>
        /// Metodo che si occupa di popolare una listbox a partire da un elenco di Versioni
        /// </summary>
        /// <param name="list">ListBox da popolare</param>
        /// <param name="versions">Collezione di versioni</param>
       static public void PopulateListBox(ListBox list, IEnumerable<Version> versions)
       {
           list.Items.Clear();

           foreach (Version v in versions)
           {
               ListBoxItem i = new ListBoxItem(){ Content=v.ToString(), Tag=v};
               list.Items.Add(i);
           }

           list.Visibility = System.Windows.Visibility.Visible;
       }

    }
}
