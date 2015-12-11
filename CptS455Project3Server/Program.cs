//This code was adapted from the msdn guide on aysnc server
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

// State object for reading client data asynchronously
public class StateObject
{
    // Client  socket.
    public Socket workSocket = null;
    // Size of receive buffer.
    public const int BufferSize = 1024;
    // Receive buffer.
    public byte[] buffer = new byte[BufferSize];
    // Received data string.
    public StringBuilder sb = new StringBuilder();
}

public class AsynchronousSocketListener
{
    // Thread signal.
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    public AsynchronousSocketListener()
    {
    }

    public static void StartListening()
    {
        // Data buffer for incoming data.
        byte[] bytes = new Byte[1024];
        int portNumber = 11000;
        // Establish the local endpoint for the socket.
        // The DNS name of the computer
        IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
        IPAddress ipAddress = ipHostInfo.AddressList[0];
        Console.WriteLine(string.Format("Proxy server on address:{0} and port:{1}",ipAddress, portNumber));
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, portNumber);

        // Create a TCP/IP socket.
        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.
                allDone.WaitOne();
                Console.WriteLine("ready for new connection");
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.
        allDone.Set();
        Console.WriteLine("start connection");

        // Get the socket that handles the client request.
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;

        // Retrieve the state object and the handler socket
        // from the asynchronous state object.
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket. 
        int bytesRead = handler.EndReceive(ar);

        if (bytesRead > 0)
        {
            try
            {
                // There  might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read 
                // more data.
                content = state.sb.ToString();

                string[] splitContent = content.Split('\n');

                Console.WriteLine("Read {0} bytes from socket. \nData : {1}",
                        content.Length, content);
                Console.WriteLine("End of Data");

                string responseFromServer;

                //checks type of request
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
                    // Display the content.

                    //response from request
                    //commented out to make output easier to read
                    //Console.WriteLine(responseFromServer);
                    // Clean up the streams and the response.
                    reader.Close();
                    response.Close();
                }
                else
                {
                    responseFromServer = "Was not get";
                }
                //parse the request


                //Send(handler, content);

                Send(handler, responseFromServer);
                /*
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);
                    // Echo the data back to the client.
                    Send(handler, content);
                }
                else
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }*/
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

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            //may need to change this so we can handle http1.1
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
            Console.WriteLine("end connecction");

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }


    public static int Main(String[] args)
    {
        StartListening();
        return 0;
    }
}