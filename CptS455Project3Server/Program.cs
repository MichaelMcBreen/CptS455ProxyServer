using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Linq;

//This project is a proxy server for HTTP requests
//It handles multiple clients at once though the use of asyn commands
//The main connection server has an endless loop for accepting 
//basic skelton based on msdn guide on asyc server
public class AsyncSocketListen
{
    // signal for thread
    public static ManualResetEvent reset = new ManualResetEvent(false);

    public static void StartListen()
    {
        int portNumber = 23456;
        IPHostEntry ipHostEntry = Dns.Resolve(Dns.GetHostName());
        IPAddress ipAddress = ipHostEntry.AddressList[0];
        Console.WriteLine(string.Format("Proxy server on address:{0} and port:{1}",ipAddress, portNumber));
        IPEndPoint localPoint = new IPEndPoint(ipAddress, portNumber);

        Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //bind listen socket for incoming connections
        try
        {
            listenSocket.Bind(localPoint);
            listenSocket.Listen(10);

            //endless loop for accepting client connections
            while (true)
            {
                //set thread signal
                reset.Reset();

                //begin async listen for sockets
                Console.WriteLine("Acepting new coonecctions");
                listenSocket.BeginAccept(
                    new AsyncCallback(StartClientAsync),
                    listenSocket);

                // Wait until a connection is made before continuing.
                reset.WaitOne();
                Console.WriteLine("Ready for new connection");
            }

        }
        //catch errors from accepting requests
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void StartClientAsync(IAsyncResult ar)
    {
        //sends to main thread to contiune
        reset.Set();
        Console.WriteLine("start connection");

        //sets up client socket
        Socket clientSocket = (Socket)ar.AsyncState;
        Socket socketHandler = clientSocket.EndAccept(ar);

        //set up new socket object
        SocketDataObject socketObject = new SocketDataObject();
        socketObject.socket = socketHandler;
        socketHandler.BeginReceive(socketObject.buf, 0, SocketDataObject.bufSize, 0,
            new AsyncCallback(ReadCallback), socketObject);
    }

    public static void ReadCallback(IAsyncResult asyncResult)
    {
        String content = String.Empty;
        SocketDataObject socketObject = (SocketDataObject)asyncResult.AsyncState;
        Socket handler = socketObject.socket;

        //read in data from socket
        int bytesRead = handler.EndReceive(asyncResult);

        if (bytesRead > 0)
        {
            try
            {
                //convert data read to ascii
                content= Encoding.ASCII.GetString(socketObject.buf, 0, bytesRead);
                
                //split data into diffetent lines
                string[] splitContent = content.Split('\n');

                Console.WriteLine("Read {0} bytes from socket\n{1}",
                        content.Length, content);
                Console.WriteLine("end of socket data");

                string responseFromServer = "invalid request type";

                //checks type of request
                if (splitContent[0].StartsWith("POST"))
                {
                    //POST http://www.google.com/gen_204?atyp=i&ct=slh&cad=&ei=qAlrVv_dBNKujwPEm7GYBA&s=4&v=2&pv=0.5987424265144127&me=12:1449855568956,H,29,i:1,H,27,i:19,H,29,o:11,H,27,o:13239,x&zx=1449855582226 HTTP/1.1
                    // Create a request for the URL. 
                    string requestURL = splitContent[0].Split(' ')[1];

                    Console.WriteLine(string.Format("Trying to reach:{0}", requestURL));

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);
                    string postContent = string.Empty;
                    request.Method = "POST";
                    if (splitContent.FirstOrDefault(x => x.StartsWith("Content-Length:")) != null)
                    {
                        request.ContentLength = Convert.ToInt64(splitContent.First(x => x.StartsWith("Content-Length:")).Split(' ')[1]);
                        if(request.ContentLength > 0)
                        {

                        }
                    }

                    int indexOfContent = -1;

                    for(int i =0; i< splitContent.Count(); i++)
                    {
                        if (splitContent[i] == "\r")
                        {
                            indexOfContent = i;
                        }
                    }
                    byte[] data = new byte[0];
                    if (indexOfContent != -1 && (indexOfContent + 1) <= splitContent.Count())
                    {

                        data = Encoding.ASCII.GetBytes(splitContent[indexOfContent + 1].TrimEnd('\r'));
                        request.ContentLength = data.Length;
                    }

                    if (splitContent.FirstOrDefault(x => x.StartsWith("Content-Type:")) != null)
                    {
                        request.ContentType = splitContent.First(x => x.StartsWith("Content-Type:")).Split(' ')[1].TrimEnd('\r');
                    }


                    request.Credentials = CredentialCache.DefaultCredentials;

                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }

                    var response = (HttpWebResponse)request.GetResponse();

                    var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                }
                if (splitContent[0].StartsWith("GET"))
                {
                    //does with get request
                    // Create a request for the URL. 
                    string requestURL = splitContent[0].Split(' ')[1];

                    Console.WriteLine(string.Format("Trying to reach:{0}", requestURL));

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);

                    request.Method = "GET";

                    if(splitContent.FirstOrDefault(x => x.StartsWith("Connection:")) != null)
                    {
                        request.KeepAlive = splitContent.First(x => x.StartsWith("Connection:")).Split(' ')[1] == "keep-alive\r";
                    }

                    if (splitContent.FirstOrDefault(x => x.StartsWith("Accept:")) != null)
                    {
                        request.Accept = splitContent.First(x => x.StartsWith("Accept:")).Split(' ')[1].TrimEnd('\r');
                    }

                    if (splitContent.FirstOrDefault(x => x.StartsWith("Host:")) != null)
                    {
                        request.Host = splitContent.First(x => x.StartsWith("Host:")).Split(' ')[1].TrimEnd('\r');
                    }

                    if (splitContent.FirstOrDefault(x => x.StartsWith("User-Agent:")) != null)
                    {
                        request.UserAgent = splitContent.First(x => x.StartsWith("User-Agent:")).Substring(11).TrimEnd('\r');
                    }

                    if (splitContent.FirstOrDefault(x => x.StartsWith("Referer:")) != null)
                    {
                        request.Referer = splitContent.First(x => x.StartsWith("Referer:")).Split(' ')[1].TrimEnd('\r');
                    }

                    request.Credentials = CredentialCache.DefaultCredentials;

                    Console.WriteLine(string.Format("\n\nour request {0}", request.ToString()));

                    // Get the response.
                    WebResponse response = request.GetResponse();
                    // Display the status.
                    //Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                    // Get the stream containing content returned by the server.
                    Stream dataStream = response.GetResponseStream();
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.
                    responseFromServer = reader.ReadToEnd();
                    reader.Close();
                    response.Close();
                }
            
                Send(handler, responseFromServer);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Send(handler, "");
            }
        }
    }

    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult asyncResult)
    {
        try
        {
            //create socket from asyncResult
            Socket socketHandler = (Socket)asyncResult.AsyncState;
            // Complete sending the data to the remote device.
            int bytesSent = socketHandler.EndSend(asyncResult);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            socketHandler.Shutdown(SocketShutdown.Both);
            socketHandler.Close();
            Console.WriteLine("end connecction");

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    //start point of program
    public static int Main(String[] args)
    {
        StartListen();
        return 0;
    }
}

public class SocketDataObject
{
    public Socket socket = null;

    public const int bufSize = 1024;

    public byte[] buf = new byte[bufSize];
}