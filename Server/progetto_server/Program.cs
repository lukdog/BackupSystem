using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
namespace Progetto_Server
{
    /// <summary>
    /// Classe del Programa principale
    /// </summary>
    class Program
    {

        /*Istruzioni per il catching dell'evento closing*/
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        static private  Server s = null;
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        /// <summary>
        /// Funzione Main per questo programma, si occupa di inizializzare e far partire il Server, 
        /// Il Server sarà in ascolto su tutti gli IP disponibili e su una porta TCP definita dal programma: 8888
        /// qualora non fosse possibile utilizzare la porta definita il programma farà un nuovo tentativo con una porta random
        /// </summary>
        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            UInt32 port = 8888;
            IPEndPoint endP = new IPEndPoint(IPAddress.Any, (Int32)port);
            Console.WriteLine("-> Inizializzo Connessione");
            TcpListener sock = null;

            int ctrl = 0;
            while (ctrl <= 1)
            {
                try
                {
                    sock = new TcpListener(endP);
                    sock.Start();
                    break;
                }
                catch (Exception)
                {
                    if (ctrl < 1)
                    {
                        Console.WriteLine("\tLa Porta Standard 8888 è già in uso, nuovo tentativo con porta Random");
                        endP = new IPEndPoint(IPAddress.Any, 0);
                        ctrl++;
                    }
                    else
                    {
                        Console.WriteLine("Impossibile Inizializzare la Connessione in Entrata");
                        return;
                    }
                }
            }

            if (sock == null) return;
            startMsg((UInt32)((IPEndPoint)sock.LocalEndpoint).Port);

            s = new Server(sock);
            s.start();
            while (true) ;

        }

        /// <summary>
        /// Metodo che stampa su Console il Numero di Porta e gli IP su cui è in ascolto il server
        /// </summary>
        /// <param name="port">Porta su cui il server è in ascolto</param>
        static void startMsg(UInt32 port)
        {
            try
            {
                string hostname;

                hostname = Dns.GetHostName();

                IPHostEntry ipEntry = Dns.GetHostEntry(hostname);
                IPAddress[] addresses = ipEntry.AddressList;

                Console.WriteLine("\tIl Server " + hostname + " è disponibile ai seguenti IP alla porta " + port + ":");
                Console.WriteLine("\t- IP Address n.0 = 127.0.0.1 ");

                for (int i = 0, j = 1; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (addresses[i].ToString() != "127.0.0.1")
                        {
                            Console.WriteLine("\t- IP Address n.{0} = {1} ", j, addresses[i].ToString());
                            j++;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Impossibile Determinare gli indirizzi IP per questo server");
            }
        }

        /// <summary>
        /// Handler dell'evento Closing
        /// </summary>
        /// <param name="ctrlType">Tipo di Messaggio di chiusura</param>
        /// <returns>True se pronto per la chiusura</returns>
        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            Console.WriteLine("Attendo Terminazione dei Task Importanti...");
            if (s != null)
            {
                s.shouldStop = true;
                s.waitForThreads();
            }
            
            return true;
        }

    }


}
