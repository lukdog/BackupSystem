using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progetto_Client
{
    /// <summary>
    /// Classe che crea una Queue ThreadSafe utile per code di Task in cui ci sono produttori e consumatori.
    /// I consumatori rimangono in attesa se la coda è vuota. Questa Coda inoltre permette di essere chiusa.
    /// </summary>
    /// <typeparam name="T">Tipo di Oggetto Contenuto</typeparam>
    class BlockingQueue<T>
    {
        private Queue<T> queue = null;
        private bool _closed = true;


        /// <summary>
        /// Costruttore di Default per la Classe BlockingQueue
        /// </summary>
        public BlockingQueue()
        {
            queue = new Queue<T>();
            _closed = false;
        }

        /// <summary>
        /// Metodo per aggiungere un elemento in coda.
        /// lancia un'eccezione se si prova ad aggiungere ad una lista chiusa.
        /// </summary>
        /// <param name="item">Elemento da Aggiungere</param>
        public void enqueue(T item)
        {
            if (_closed)
            {
                throw new Exception("Errore: la Coda è chiusa!");
            }

            lock (queue)
            {
                queue.Enqueue(item);
                System.Threading.Monitor.Pulse(queue);
            }

            

        }

        /// <summary>
        /// Metodo per rimuovere un elemento dalla coda, 
        /// </summary>
        /// <param name="element">Elemento su cui salvare l'elemento rimosso</param>
        /// <returns>true se estrazione riuscita, false se la coda è vuota e chiusa.</returns>
        public bool dequeue(out T element)
        {
            bool res = false;
            lock (queue)
            {
                while (this.empty && !this.closed)
                {
                    System.Threading.Monitor.Wait(queue);
                }

                if (!this.empty)
                {
                    element = queue.Dequeue();
                    res = true;
                }
                else
                {
                    element = default(T);
                }
            }

            return res;

        }

        /// <summary>
        /// proprietà Bool che offre il solo metodo get e ritorna se la coda è vuota.
        /// </summary>
        public bool empty
        {
            get { return queue.Count == 0; }
        }

        /// <summary>
        /// Proprietà che permette di vedere se la coda è chiusa e di settarla chiusa o aperta.
        /// </summary>
        public bool closed
        {
            get { return _closed; }
            set {
                lock (queue)
                {
                    _closed = value;
                    System.Threading.Monitor.PulseAll(queue);
                }
                
            }
        }

    }
}
