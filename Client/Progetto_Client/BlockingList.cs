using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progetto_Client
{
    /// <summary>
    /// Lista Bloccante thread-safe in cui è possibile solo aggiungere elementi fino alla sua chiusura quando viene prelevata la lista.
    ///// la lista può essere prelevata una volta sola. 
    /// </summary>
    /// <typeparam name="T">Tipo dell'oggetto contenuto in lista</typeparam>
    class BlockingList<T>
    {
        private List<T> list = null;
        private bool _closed = true;

        /// <summary>
        /// Costruttore di Default per la Classe Blockinglist
        /// </summary>
        public BlockingList()
        {
            list = new List<T>();
            _closed = false;
        }

        /// <summary>
        /// Costruttore che permette di Creara una BlockingList a partire da una lista di valori di un dizionario che usa come chiave una String
        /// </summary>
        /// <param name="dic">Dizionario da cui prelevare i valori, deve avere una String come chiave</param>
        public BlockingList(ConcurrentDictionary<String, T> dic)
        {
            foreach (String k in dic.Keys)
            {
                list.Add(dic[k]);
            }
            _closed = false;
        }

        /// <summary>
        /// Metodo per aggiungere un elemento in lista.
        /// lancia un'eccezione se si prova ad aggiungere ad una lista chiusa.
        /// </summary>
        /// <param name="item">Elemento da Aggiungere</param>
        public void add(T item)
        {
            if (_closed)
            {
                throw new Exception("Errore: la lista è chiusa!");
            }

            lock (list)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// proprietà Bool che offre il solo metodo get e ritorna se la lista è vuota.
        /// </summary>
        public bool empty
        {
            get { return list.Count == 0; }
        }

        /// <summary>
        /// Proprietà che permette di vedere se la lista è chiusa
        /// </summary>
        public bool closed
        {
            get { return _closed; }
        }

        /// <summary>
        /// Metodo che permette di prelevare la lista, questa però può essere prelevata una volta sola.
        /// </summary>
        /// <returns>Lista di oggetti di tipo T</returns>
        public List<T> getList()
        {
            lock(list){
                if (closed) return null;
                _closed = true;
                return list;
            }
        }
    }
}
