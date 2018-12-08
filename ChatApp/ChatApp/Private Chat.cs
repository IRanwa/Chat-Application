using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace ChatApp
{
    public partial class Private_Chat : Form
    {
        private String clntName; //other client username
        private String username; //client username
        private String endpoint; //Other client  endpoint
        private TcpClient tcpclnt; //tcpclient
        private NetworkStream stm; //Network stream

        //Getters and setters
        public string ClntName {set => clntName = value; }
        public string Username { set => username = value; }
        public string Endpoint { get => endpoint; set => endpoint = value; }
        public TcpClient Tcpclnt { set => tcpclnt = value; }
        public NetworkStream Stm { set => stm = value; }

        public Private_Chat()
        {
            InitializeComponent();
        }

        //Set private chat log
        public void setStatus(string msg)
        {
            txtStatus.Text += msg + "\n";
            txtStatus.Select(txtStatus.Text.Length - 1, 0); //Cursor move to end od the text
            txtStatus.ScrollToCaret(); //Scroll to the cursor position
        }
        
        //When private chat form open change the title
        private void Private_Chat_Load(object sender, EventArgs e)
        {
            this.Text = "Private Chat [ " + clntName + " ]";
        }

        //Private chat send button click
        private void btnSend_Click(object sender, EventArgs e)
        {
            string msg = txtMessage.Text+"\n";
            sendPrvMsg("?P"+Endpoint + "$" + msg); //Send private chat message to the specific endpoint
            setStatus(username + " : " + msg);
            txtMessage.Clear();
        }

        private void sendPrvMsg(string msg)
        {
            if (tcpclnt.Connected) //Check if the tcpclient connected
            {
                //Private message send to through the network stream
                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(msg);
                Console.WriteLine("Private Transmitting.....");
                stm = tcpclnt.GetStream();
                stm.Write(ba, 0, ba.Length);
                stm.Flush();
                Console.WriteLine("Private Transmitted!");
            }
        }
        
    }
    
}
