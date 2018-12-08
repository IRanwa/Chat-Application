using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections;

namespace Server
{
    public partial class Server : Form
    {
        private TcpListener tcpListener;//TcpListener
        private Hashtable cList; //Clients List (Client socket and name)
        private List<string> messages; //Group chat previous messages
        private static readonly Object receiverLock = new Object(); 

        public Server()
        {
            InitializeComponent();
        }

        //Get Local IP address
        private static string GetLocalIPAddress()
        {
            //Using hostname getting all the ip address and return only one ipv4 address
            var host = Dns.GetHostEntry(Dns.GetHostName());

            String ipAddress = null;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("Local IP Address : " + ip.ToString());
                    ipAddress = ip.ToString();
                }
            }
            return ipAddress;
        }

        //Set Message to the Server Log
        private void setStatus(String status)
        {
            //Invoke used because of the cross-threading 
            if (txtStatus.InvokeRequired)
            {
                this.Invoke(new Action<string>(setStatus), new object[] { status });
                return;
            }
            txtStatus.Text += status + "\n";
            Console.WriteLine(status + "\n");

            //Server Log cursor will move to the end of the text on server log
            txtStatus.Select(txtStatus.Text.Length - 1, 0);
            //Scroll bar will scroll to the cursor position
            txtStatus.ScrollToCaret(); 
        }

        //Start Server
        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                String ip = GetLocalIPAddress(); //Getting local ip address
                if (!ip.Equals(null)) //Check if the return ip is not null
                {
                    //Parsing ip address String to IPAddress and creating a local server using the port 8001
                    tcpListener = new TcpListener(IPAddress.Parse(ip), 8001);
                    tcpListener.Start(); //Starting the server

                    setStatus("The server is running at " + ip);
                    setStatus("Waiting for a connections.....");

                    serverStarted();
                }
                else
                {
                    //If the local IP address not received from the system. Display this message
                    MessageBox.Show("Receiving local IP error. Try again!","Local IP Error"
                        , MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Console.WriteLine("Receiving local IP error. Try again!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Server starting error.", "Error", MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                Console.WriteLine("Server starting error : " + ex.StackTrace);
            }
        }

        //After tcplistener started server will accept clients and disable / enable some buttons on the form
        private void serverStarted()
        {
            messages = new List<string>(); //Initializing a array to keep previous messages
            cList = new Hashtable(); //Intializing a hashtable of clients

            btnStart.Enabled = false; //Start Button disabled because server started successfully!
            btnStop.Enabled = true; //Stop Button enabled. So, the admin can stop the server
            btnRClnt.Enabled = true;//Remove Client button enabled. So, the admin can remove client from the server

            new Thread(clientAccept_Thread).Start(); //Starting clients accepting thread
        }
        
        //Clients Accepting
        private void clientAccept_Thread()
        {
            //Used to check if the client accepting done without exceptions
            //If the socket disconnected exception will be thrown
            Boolean flag = false; 
            try
            {
                //Waiting for the client to connect
                Socket s = tcpListener.AcceptSocket();

                //Client has been accepted from the server and getting client messages
                sendLatestMsg(s); //Start thread to send previous messages to the new client
                Thread.Sleep(750);
                new Thread(() => receiveClient_Msgs(s)).Start(); //Start thread to accept client messages
                

                EndPoint e = s.RemoteEndPoint; //New client endpoint ip with port
                setStatus("Connection accepted from " + e);
                flag = true;
            }
            catch (Exception ex)
            {
                if (tcpListener.Server.Connected) //Check if the tcpListener connected or not
                {
                    setStatus("Client connecting error!");
                }
            }
            if (flag) {
                //Client accepting thread start again. So, more clients can connect parallel
                new Thread(clientAccept_Thread).Start(); 
            }
        }
         
        //Receive Client messages
        private void receiveClient_Msgs(Socket s)
        {
            //Used to check if the client msg receive done without exceptions
            //If the tcplistener disconnected exception will be thrown
            Boolean flag = false;
            try
            {
                byte[] b = new byte[Byte.MaxValue]; //100 bytes of array
                
                int k = s.Receive(b); //Waiting for a client message to receive
                //Locking mechanism used to block incoming messages parallel. 
                lock (receiverLock) {
                    if (k != 0) 
                    {
                        setStatus(s.LocalEndPoint + " Recieving...");

                        //Received message as bytes converting to a string
                        String text = "";
                        for (int i = 0; i < k; i++)
                        {
                            text += Convert.ToChar(b[i]);
                        }
                        //Receive message contain some tag. receiveMsgExtract will remove tag and get the msg
                        receivedMsgExtract(text, s);
                    }
                    flag = true;
                }
            }
            catch (Exception ex)
            {
                if (s.Connected)
                {
                    setStatus("Client connection error!");
                }
            }
            if (flag) {
                //Receive client messages thread start again to receive one client messages over and over
                new Thread(() => receiveClient_Msgs(s)).Start();
            }
        }

        //Removing tag in the message
        private void receivedMsgExtract(string text,Socket s)
        {
            EndPoint remoteEndPoint = s.RemoteEndPoint;
            string msg = text.Substring(2); //Remove first 2 character tag
            if (text.IndexOf("?N")==0) //If new client connected
            {
                Boolean clientStatus = addClient(s, msg); //Client will add to the list of clients connect to the server
                //If the number of clients connected to the server is used. Then the Client cannot connect anymore. 
                if (!clientStatus)
                {
                    setStatus("Client cannot add! Exceeded the maximum users");
                    s.Close(); //Closing the socket
                }
                else
                {
                    updateOnlineClients(s, msg); //Newly conncted client details will pass to the connected clients
                }
            }else if (text.IndexOf("?G")==0) //Received group message
            {
                sendGroupMsg(msg, s); //Send message to all the connected clients
                //Only keep 32 to previous messages in the server. If it exceed then remove the oldest message form server.
                if (messages.Count == 32)
                {
                    messages.RemoveAt(0);
                }
                msg = cList[s] + " : " + msg+"\n";
                messages.Add(msg);
            }
            else if (text.IndexOf("?P")==0) //Receive private message
            {
                string[] split = msg.Split('$'); 
                string ip = split[0]; //get ip address of the receiver
                msg = msg.Substring(ip.Length + 1); //get the message
                sendPrivateMsg(ip,msg, remoteEndPoint); //Send the private message only to the specific client
            }
            else if(text.IndexOf("?E")==0)
            {
                setStatus(remoteEndPoint + " Client Disconnecting...");
                updateOnlineClients(s); //Other clients will notify this client disconnecting from the server
                remClient(s); //Remove client from the server
                setStatus("Client Disconnected!");
            }
            else
            {
                sendGroupMsgExtra(text,s); //Send message to all the connected clients
                //Only keep 32 to previous messages in the server. If it exceed then remove the oldest message form server.
                if (messages.Count == 32)
                {
                    messages.RemoveAt(0);
                }
                msg =  msg + "\n";
                messages.Add(msg);
            }
            setStatus(remoteEndPoint + " Recieved...");
        }

        //Add Clients to the online user list
        private Boolean addClient(Socket s,string clientName)
        {
            if (cList.Count<30)
            {
                cList.Add(s, clientName);
                
                this.clientsList.Invoke((MethodInvoker)delegate()
                {
                    clientsList.Items.Add(clientName); //Adding client to the online clients list box

                });
                
                return true;
            }
            return false;
        }

        //Remove client from online users
        private void remClient(Socket s)
        {
            this.clientsList.Invoke((MethodInvoker)delegate ()
            {
                clientsList.Items.Remove(cList[s]); //Remove client from the online clients list box

            });
            s.Close();
            cList.Remove(s);
        }

        //sending previous 32 messages
        private void sendLatestMsg(Socket s)
        {
            //Combining all the 32 messages to one
            string sendMsg = "";
            foreach (string msg in messages) {
                sendMsg += msg;
            }
            //String message convert to bytes and send to the newly connected client through the network stream
            ASCIIEncoding asen = new ASCIIEncoding();
            s.Send(asen.GetBytes("?LM"+sendMsg));
            setStatus("Lastest messages sent to client");
        }

        //Send message to all the online users
        private void sendGroupMsg(String msg,Socket s)
        {
            //Sending message to each and every client connected to the server
            foreach (Socket client in cList.Keys)
            {
                if (client!=s) {
                    ASCIIEncoding asen = new ASCIIEncoding(); //ASCII encoding
                    //Message convert to bytes and send to every online client
                    client.Send(asen.GetBytes("?GM"+s.RemoteEndPoint + "$" + msg));
                }
            }
            setStatus("Msg Sent to the group chat from "+s.RemoteEndPoint);
        }

        private void sendGroupMsgExtra(String msg,Socket s)
        {
            //Sending message to each and every client connected to the server
            foreach (Socket client in cList.Keys)
            {
                if (client!=s) {
                    ASCIIEncoding asen = new ASCIIEncoding(); //ASCII encoding
                    //Message convert to bytes and send to every online client
                    client.Send(asen.GetBytes(msg));
                }
            }
            setStatus("Extra Msg Sent to the group chat from "+s.RemoteEndPoint);
        }
        //Send message to a specific user
        private void sendPrivateMsg(string endPoint,string msg,EndPoint senderEndpoint)
        {
            //Find the specific client and send the msg
            foreach (Socket client in cList.Keys)
            {
                if (client.RemoteEndPoint.ToString().Equals(endPoint))
                {
                    //Message convert to bytes and send it through the network stream
                    ASCIIEncoding asen = new ASCIIEncoding();
                    client.Send(asen.GetBytes("?PM" + senderEndpoint + "$" + msg));
                    setStatus("Msg Sent to the private chat");
                    break;
                }
            }
        }

        //Notify online client disconnected from the server
        private void updateOnlineClients(Socket s)
        {
            foreach (Socket client in cList.Keys)
            {
                if (client!=s) {
                    ASCIIEncoding asen = new ASCIIEncoding();
                    client.Send(asen.GetBytes("?EC" + s.RemoteEndPoint));
                }
            }
            setStatus("Notifying "+s.RemoteEndPoint+" client disconnecting...");
        }

        //Newly connected cliet will get all the online users as well as other clients get the newly connected client details
        private void updateOnlineClients(Socket newClient,String newClientName)
        {
            ASCIIEncoding asen;
            String text = "";

            //Getting all the connected clients details
            foreach (Socket client in cList.Keys)
            {
                if (client!=newClient)
                {
                    asen = new ASCIIEncoding();
                    client.Send(asen.GetBytes("?NC" + newClient.RemoteEndPoint + "#" + newClientName));
                    
                    //Getting all the clients connected to the server
                    text += client.RemoteEndPoint + "#" + cList[client] + "/";
                }
            }
            setStatus("New client endpoint sent to all the connected clients");

            //Send all the connect client details to the new client
            if (text.Length!=0) {
                //lock (receiverLock) {
                    asen = new ASCIIEncoding();
                    newClient.Send(asen.GetBytes("?CL" + text));
                    setStatus("Client list send to the new client");
               // }
            }
        }

        //Server stop
        private void btnStop_Click(object sender, EventArgs e)
        {
            serverDisconnect();

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnRClnt.Enabled = false;
            clientsList.Items.Clear(); //Clear the online list of clients
            
        }

        //Server will send a message to clients. Server going to disconnect
        private void serverDisconnect()
        {
            //Inform all the connected clients server disconnecting.
            foreach (Socket client in cList.Keys)
            {
                ASCIIEncoding asen = new ASCIIEncoding();
                client.Send(asen.GetBytes("?SE"));
                client.Close();
            }

            //End server coonnection
            if (tcpListener != null)
            {
                tcpListener.Stop(); //Stopping the server
            }
            setStatus("Server Stopped!");
        }
        //Form closing action
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cList!=null) {
                serverDisconnect();
            }
            Environment.Exit(0); //Program close
        }

        //Remove client from the server
        private void btnRClnt_Click(object sender, EventArgs e)
        {
            //Get selected user name from the online client list
            string name = clientsList.SelectedItem.ToString();
            if (name!=null)
            {
                remClient(name);
                clientsList.SelectedItem = false;
            }
            else
            {
                MessageBox.Show("Please select a client!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        //Send message to the specific client to disconnect
        private void remClient(String name)
        {
            //Getting client socket and sending the message to the client to disconnect
            Socket client = null;
            foreach (Socket s in cList.Keys)
            {
                if (cList[s].Equals(name))
                {
                    client = s;
                    ASCIIEncoding asen = new ASCIIEncoding();
                    client.Send(asen.GetBytes("?SE"));
                    break;
                }
            }
            //When message to send to the specific client updatte other client online list and remove client from the server
            if (client != null)
            {
                lock (receiverLock) {
                    setStatus(client.RemoteEndPoint + " Client Disconnecting...");
                    updateOnlineClients(client); //Other clients will notify this client disconnecting from the server
                    remClient(client); //Remove client from the server
                    setStatus("Client Disconnected!");
                }
            }
        }
    }
}
