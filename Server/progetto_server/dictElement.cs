using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;

namespace Progetto_Server
{
    /// <summary>
    /// Classe generica utile per serializzare un Dictionary tramite XML
    /// Tramite questa classe, quando è il caso di serializzare salvo tutte le coppie chiave valore in nuove istanze di questa classe.
    /// Per deserializzare prendo i dati da qui e li salvo nel nuovo Dictionary
    /// </summary>
    /// <typeparam name="T">Tipo di Oggetto della chiave</typeparam>
    /// <typeparam name="W">Tipo di Oggetto del valore</typeparam>
    public class dictElement<T, W>
    {
        /// <summary>
        /// Attributo Chiave della coppia (chiave|valore)
        /// </summary>
        public T key;
        /// <summary>
        /// Valore corrispondente alla chiave
        /// </summary>
        public W value;

        /// <summary>
        /// Costruttore per creare un'istanza di questa classe a partire da una coppia chiave-valore
        /// </summary>
        /// <param name="k">Chiave o Indice della Coppia</param>
        /// <param name="v">Valore</param>
        public dictElement(T k, W v)
        {
            key = k;
            value = v;
        }

        public dictElement() { }


        /// <summary>
        /// Metodo Statico per la serializzazione di un Dictionary
        /// </summary>
        /// <param name="stream">Stream su cui effettuare la Serializzazione</param>
        /// <param name="dic">Dictionary da Serializzare</param>
        /// <returns></returns>
        public static bool serializeTo(Stream stream, Dictionary<T, W> dic)
        {
            List<dictElement<T, W>> myList = new List<dictElement<T, W>>(dic.Count);

            foreach (T k in dic.Keys)
            {
                myList.Add(new dictElement<T, W>(k, dic[k]));
            }

            XmlSerializer ser = new XmlSerializer(typeof(List<dictElement<T, W>>));

            try
            {
                ser.Serialize(stream, myList);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Metodo Statico per la serializzazione di un Dictionary,
        /// Overload per Concurrent Dictionary
        /// </summary>
        /// <param name="stream">Stream su cui effettuare la Serializzazione</param>
        /// <param name="dic">ConcurrentDictionary da Serializzare</param>
        /// <returns></returns>
        public static bool serializeTo(Stream stream, ConcurrentDictionary<T, W> dic)
        {
            List<dictElement<T, W>> myList = new List<dictElement<T, W>>(dic.Count);

            foreach (T k in dic.Keys)
            {
                myList.Add(new dictElement<T, W>(k, dic[k]));
            }

            XmlSerializer ser = new XmlSerializer(typeof(List<dictElement<T, W>>));

            try
            {
                ser.Serialize(stream, myList);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Metodo Statico per la deserializzazione di un Dictionary ricevuto tramite Stream.
        /// </summary>
        /// <param name="stream">Stream da cui deserializzare, ad esempio un networkStream</param>
        /// <returns>Istanza della Classe Dictionary con i dati letti dallo stream</returns>
        public static Dictionary<T, W> deserializeFrom(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(List<dictElement<T, W>>));
            List<dictElement<T, W>> myList = null;
            try
            {
                myList = (List<dictElement<T, W>>)ser.Deserialize(stream);
            }
            catch(Exception)
            {
                return null;
            }

            Dictionary<T, W> myDic = new Dictionary<T, W>();

            foreach (dictElement<T, W> item in myList)
            {
                myDic[item.key] = item.value;
            }

            return myDic;

        }

        /// <summary>
        /// Metodo Statico per la deserializzazione di un Dictionary ricevuto tramite Stream.
        /// Overload per concurrent Dictionary
        /// </summary>
        /// <param name="stream">Stream da cui deserializzare, ad esempio un networkStream</param>
        /// <returns>Istanza della Classe Concurrent Dictionary con i dati letti dallo stream</returns>
        public static ConcurrentDictionary<T, W> deserializeFromConcurrent(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(List<dictElement<T, W>>));
            List<dictElement<T, W>> myList = null;
            try
            {
                myList = (List<dictElement<T, W>>)ser.Deserialize(stream);
            }
            catch (Exception)
            {
                return null;
            }

            ConcurrentDictionary<T, W> myDic = new ConcurrentDictionary<T, W>();

            foreach (dictElement<T, W> item in myList)
            {
                myDic[item.key] = item.value;
            }

            return myDic;

        }
        


    }
}
