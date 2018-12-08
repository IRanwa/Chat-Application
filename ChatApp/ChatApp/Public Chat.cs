using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.Collections;

namespace ChatApp
{
    public partial class Form1 : Form
    {
        private TcpClient tcpclnt; //TcpClient
        private string username; //Client username
        private NetworkStream stm; //Network Stream
        private Hashtable onlineUsers; //Online client list (client endpoint and the name)
        private Hashtable pcUsers; //Private chats
        private List<string> endpoints; //List of end points (This used to identify 
        private static readonly object locker = new object();
        
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }
        
        //Send Message
        private void btnSend_Click(object sender, EventArgs e)
        {
            string msg = txtMessage.Text; //Get entered text message
            if (msg.Length!=0) {
                if (msg.Contains('\n'))
                {
                    msg = msg.Replace('\n','\t');
                    
                }
                updateChatLog(username + " : " + msg+"\n\n"); //Setting message to the chat log
                msg = "?G" + msg + "\n";
                new Thread(()=> sendMsg(msg)).Start(); //Sending message start
                txtMessage.Clear();
            }
        }

        //Update chat log text
        private void updateChatLog(String status)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(updateChatLog), new object[] { status });
                return;
            }
            txtStatus.Text += status;
            
            txtStatus.Select(txtStatus.Text.Length - 1, 0); //Cursor move to the end of the text
            txtStatus.ScrollToCaret(); //Scroll down to the cursor position
        }

        //Online users list double click to open private chat
        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (usersList.SelectedItem!=null) //Check if the selected online user is null
            {
                //This used to check if the private chat visible. 
                //If not new private chat for the specific client will open
                Boolean flag = true; 
                //Get index of the selected client
                int userIndex = usersList.SelectedIndex;
                //Get endpoint of a specific client
                string selectUser = endpoints[endpoints.Count-userIndex-1];
                //check if the private chat all ready open
                foreach (Private_Chat pcUser in pcUsers.Keys)
                {
                    if (selectUser.Equals(pcUser.Endpoint))
                    {
                        if (pcUser.Visible)
                        {
                            flag = false;
                            pcUser.Visible = true;
                        }
                        else
                        {
                            pcUsers.Remove(pcUser); //If form not visible remove the form
                        }
                        break;
                    }
                }
                //If the private chat not opened. Flag value will be trye.
                if (flag) {
                    //Finiding specific client details and open a new private chat with the specific selected client
                    foreach (string users in onlineUsers.Keys)
                    {
                        if (users.Equals(selectUser))
                        {
                            Private_Chat pc = new Private_Chat();
                            pc.ClntName = onlineUsers[selectUser].ToString();
                            pc.Username = username;
                            pc.Endpoint = users;
                            pc.Tcpclnt = tcpclnt;
                            pcUsers.Add(pc, users);
                            pc.Visible = true;
                            break;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Private Chat already opened", "", MessageBoxButtons.OK
                        , MessageBoxIcon.Information);
                }
            }
        }

        //When public chat closing send a message to the server client disconnecting
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tcpclnt!=null && tcpclnt.Connected)
            {
                sendMsg("?E");
                tcpclnt.Close();
            }
            Environment.Exit(0);
        }

        //Send message to the server
        private void sendMsg(String msg)
        {
            lock (locker) {
                if (tcpclnt.Connected) {
                    try
                    {
                        //Convert string msg to bytes
                        ASCIIEncoding asen = new ASCIIEncoding();
                        byte[] ba = asen.GetBytes(msg);
                        Console.WriteLine("Transmitting.....");
                        //Sending message through the network stream
                        NetworkStream stm = tcpclnt.GetStream();
                        stm.Write(ba, 0, ba.Length);
                        stm.Flush(); //Flushing will output all the data written to the network stream
                        Console.WriteLine("Transmitted!");
                    }
                    catch (Exception ex)
                    {
                        if (tcpclnt.Connected)
                        {
                            updateChatLog("Client connectiong to server error!\n\n");
                        }
                    }
                }
            }
        }

        //Receive messages from the server
        private void receiveMsg()
        {
            //To ensure that rceive message didn't give an exception
            //If exception thrown it will handle and end the thread
            Boolean flag = false; 
            try
            {
                byte[] bb = new byte[Byte.MaxValue]; //100 bytes of array
                stm = tcpclnt.GetStream(); //getting client stream
                int k = stm.Read(bb, 0, bb.Length); //Read incoming message to bytes
                if (k>0) {
                    lock (locker) {
                        //Convert message bytes to string
                        String text = "";
                        for (int i = 0; i < k; i++)
                        {
                            text += Convert.ToChar(bb[i]);
                        }
                        stm.Flush(); //Flush the network stream
                        receiveMsgExtract(text); //Remove tags and split the message
                    }
                }
                flag = true;
            }
            catch (Exception)
            {
                if (tcpclnt.Connected)
                {
                    clntDisConnected();
                    updateChatLog(">>> Connection to the server error!\n\n");
                    
                }
            }
            if (flag)
            {
                new Thread(receiveMsg).Start(); //Receive messages from server again
            }
        }

        private void receiveMsgExtract(string text)
        {
            string msg = text.Substring(3); //Remove tag from the message
            if (text.IndexOf("?CL")==0)  //Client received list of clients connected to the server
            {
                string[] users = msg.Split('/'); //Each client details sperated by '/'
                //Go through each and every client and add them to the online list
                foreach (String u in users)
                {
                    clientListUpdate(u);
                }
            }
            else if (text.IndexOf("?NC") == 0) //New client details received
            {
                clientListUpdate(msg); //update new client to the online list
            }
            else if (text.IndexOf("?GM") == 0 || text.IndexOf("?PM") == 0) //Group message or private message received
            {
                chatMsg(msg, text.Substring(0,3)); //Display the group or private message
            }
            else if (text.IndexOf("?EC") == 0) //Client disconnected from the server
            {
                //Go through each and every client and find the specific client endpoint.
                //If the client have any private chats with the closing client private chat forms will close
                foreach (string user in onlineUsers.Keys)
                {
                    if (user.Equals(msg))
                    {
                        foreach (Private_Chat pcUser in pcUsers.Keys)
                        {
                            if (pcUsers[pcUser].Equals(msg))
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    if (pcUser.Visible)
                                    {
                                        pcUser.Visible = false;
                                    }
                                });
                                break;
                            }
                        }
                        removeOnlineUsers(onlineUsers[user].ToString()); //Remove client from online list
                        onlineUsers.Remove(user); 
                        endpoints.Remove(user);
                        break;
                    }
                }
            }
            else if(text.IndexOf("?SE")==0) //Server disconnected
            {
                clntDisConnected(); //Client disconnect from the server
                updateChatLog(">> Server Stopped!\n\n");
            }
            else if (text.IndexOf("?LM")==0)
            {
                updateChatLog(msg+"\n");
            }
            else
            {
                updateChatLog(text+"\n"); //When client newly connected. Client will receive previous 32 messages.
            }
        }

        //Group and private messages forward
        private void chatMsg(string msg,string tag)
        {
            string[] split = msg.Split('$');
            string ip = split[0]; //Client endpoint
            msg = msg.Substring(ip.Length + 1); //Message from a client
            foreach (string user in onlineUsers.Keys)
            {
                if (user.Equals(ip))
                {
                    if (tag.IndexOf("?GM")==0) { //Received message is group
                        updateChatLog(onlineUsers[user] + " : " + msg+"\n");
                    }
                    else
                    {
                        openPrivateChat(ip, user, msg);
                    }
                    break;
                }
            }
        }

        //Open or set received private message to the client private chat window
        private void openPrivateChat(string ip,string user,string msg)
        {
            //Flag used to check if the client already have a private chat. If not open a new private chat
            Boolean flag = true;
            //Check if client having a private chat with the other client already
            foreach (Private_Chat pcUser in pcUsers.Keys)
            {
                if (pcUsers[pcUser].Equals(ip))
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {

                        if (pcUser.Visible)
                        {
                            flag = false;
                            pcUser.setStatus(onlineUsers[user] + " : " + msg); //Client available display received message
                        }
                        else
                        {
                            pcUsers.Remove(pcUser); //If private form not visible remove private chat window from the list
                        }

                    });

                    break;
                }
            }
            if (flag)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    //Open a new private chat
                    Private_Chat pc = new Private_Chat();
                    pc.ClntName = onlineUsers[user].ToString();
                    pc.Username = username;
                    pc.Endpoint = ip;
                    pc.Tcpclnt = tcpclnt;
                    pcUsers.Add(pc, ip);
                    pc.Visible = true;
                    pc.setStatus(onlineUsers[user] + " : " + msg);
                });

            }
        }

        //clients connected list update
        private void clientListUpdate(string user)
        {
            if (user.Length != 0)
            {
                string[] userDetails = user.Split('#');
                onlineUsers.Add(userDetails[0], userDetails[1]); //Add client to the array
                endpoints.Add(userDetails[0]);  // Add client endoints
                addOnlineUsers(userDetails[1]); //Add client to the online list box
            }
        }

        //Adding client to the online client list box
        private void addOnlineUsers(String user)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(addOnlineUsers), new object[] { user });
                return;
            }
            usersList.Items.Add(user);
        }

        //Removing client from the online client list box
        private void removeOnlineUsers(string name)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(removeOnlineUsers), new object[] { name });
                return;
            }
            usersList.Items.Remove(name);
        }

        //When client click button connect
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (txtIP.Text.Length!=0 && txtName.Text.Length!=0) //Check if the IP and username blank
            {
                username = txtName.Text; //Getting client username
                try
                {
                    pcUsers = new Hashtable();
                    onlineUsers = new Hashtable();
                    endpoints = new List<string>();

                    tcpclnt = new TcpClient();
                    Console.WriteLine("Connecting....."+txtIP.Text);
                    tcpclnt.Connect(txtIP.Text, 8001); //Connecting client to the server
                    getOnlineUsers(); //Get online client list
                    this.Text = "Group Chat [ " + username + " ]"; //Change the group chat title
                    updateChatLog(">>> Client connected!\n\n");
                }
                catch (Exception ex)
                {
                    clntDisConnected(); //If client connection give error disconnect client
                    MessageBox.Show("Server connecting error.\nError : "+ex.StackTrace
                        ,"Server Connecting Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine("Server connecting error.\nError : " + ex.StackTrace);
                }
            }
            else
            {
                MessageBox.Show("Please enter Server IP and Username!", "", MessageBoxButtons.OK
                    , MessageBoxIcon.Information);
            }
        }

        private void getOnlineUsers()
        {
            if (tcpclnt.Connected) {
                
                new Thread(receiveMsg).Start(); //Start receive message thread
                lock (locker) {
                    //send new client connected message to server
                    new Thread(() => sendMsg("?N" + txtName.Text)).Start(); 
                }

                clntConnected(); 
            }
            else
            {
                MessageBox.Show("Server connecting error.", "Error", MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
            }
        }
        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (tcpclnt!=null)
            {
                sendMsg("?E"); //Send client disconnecting message to server
                clntDisConnected();
                txtStatus.Clear();
                
                stm.Close();
                tcpclnt.Close(); //Close the tcpclient 

                updateChatLog(">>> Client disconnected!\n\n");
            }
        }

        //After client connected change button status
        private void clntConnected()
        {
            txtIP.Enabled = false;
            txtName.Enabled = false;
            btnConnect.Enabled = false;
            btnDisconnect.Enabled = true;
            txtMessage.Enabled = true;
            btnSend.Enabled = true;
        }

        //After client disconnected close all private chats and clear the online list and change button status
        private void clntDisConnected()
        {
            this.Invoke((MethodInvoker)delegate
            {
                //Close all the pivate chats
                foreach (Private_Chat pcUser in pcUsers.Keys)
                {
                    if (pcUser.Visible)
                    {
                        pcUser.Visible = false;
                    }
                }
                usersList.Items.Clear(); //clear  online list
                txtIP.Enabled = true;
                txtName.Enabled = true;
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
                txtMessage.Enabled = false;
                btnSend.Enabled = false;
            });
        }
    }
}
