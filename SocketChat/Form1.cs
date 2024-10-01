using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SocketChat
{
    public partial class Form1 : Form
    {
        private Socket listener = null;
        private bool started = false;
        private int _port = 11000;
        private static int _buff_size = 2048;
        private byte[] _buffer = new byte[_buff_size];
        private Thread serverThread = null;
        private List<Socket> clientSockets = new List<Socket>();
        private delegate void SafeCallDelegate(string text);
        public Form1()
        {
            InitializeComponent();
            listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (started)
                {
                    started = false;
                    button2.Text = "Listen";
                    serverThread = null;
                    listener.Close();
                }
                else
                {
                    serverThread = new Thread(this.listen);
                    serverThread.Start();
                   
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void listen()
        {
            listener.Bind(new IPEndPoint(IPAddress.Parse(textBox1.Text), _port));
            listener.Listen(10);
            started = true;
            //button2.Text = "Stop";
            UpdateChatHistoryThreadSafe("Start listening");
            while (started)
            {
                //Application.DoEvents();
                Socket clientSocket = listener.Accept();
                lock (clientSockets)
                {
                    clientSockets.Add(clientSocket); // Thêm client socket vào danh sách
                }
                UpdateChatHistoryThreadSafe("Accept connection from " + clientSocket.RemoteEndPoint.ToString());
                //int readbytes = client.Receive(_buffer);
                //string s = Encoding.UTF8.GetString(_buffer);
                //UpdateChatHistoryThreadSafe(s + "\n");
                if (clientSocket != null && clientSocket.Connected)
                {
                    (new Thread(() => this.readFromSocket(clientSocket))).Start();
                }
            }
        }

        private async void readFromSocket(Socket clientSocket)
        {
            while (clientSocket.Connected)
            {
                try
                {
                    int readBytes = await clientSocket.ReceiveAsync(new ArraySegment<byte>(_buffer), SocketFlags.None);
                    if (readBytes > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(_buffer, 0, readBytes);

                        if (receivedMessage.StartsWith("FILE:"))
                        {
                            // Xử lý file
                            string fileName = receivedMessage.Substring(5);
                            byte[] fileSizeBuffer = new byte[4];
                            await clientSocket.ReceiveAsync(new ArraySegment<byte>(fileSizeBuffer), SocketFlags.None);
                            int fileSize = BitConverter.ToInt32(fileSizeBuffer, 0);

                            byte[] fileData = new byte[fileSize];
                            int totalReceived = 0;

                            while (totalReceived < fileSize)
                            {
                                int received = await clientSocket.ReceiveAsync(new ArraySegment<byte>(fileData, totalReceived, fileSize - totalReceived), SocketFlags.None);
                                totalReceived += received;
                            }

                            // Lưu file vào server hoặc xử lý theo cách cần thiết
                            UpdateChatHistoryThreadSafe($"Received file: {fileName}");
                        }
                        else
                        {
                            // Nhận tin nhắn thông thường
                            UpdateChatHistoryThreadSafe(receivedMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    break;
                }
            }
        }
        private void BroadcastFile(string fileName, byte[] fileData, Socket excludeSocket)
        {
            lock (clientSockets)  // Đảm bảo an toàn thread
            {
                foreach (var socket in clientSockets)
                {
                    if (socket != excludeSocket)
                    {
                        try
                        {
                            // Gửi tên file trước
                            socket.Send(Encoding.UTF8.GetBytes("FILE:" + fileName));

                            // Gửi kích thước file
                            byte[] fileSizeBytes = BitConverter.GetBytes(fileData.Length);
                            socket.Send(fileSizeBytes);

                            // Gửi nội dung file
                            socket.Send(fileData);
                        }
                        catch (Exception ex)
                        {
                            // Xử lý khi client ngắt kết nối
                            clientSockets.Remove(socket);
                            socket.Close();
                        }
                    }
                }
            }
        }
        void BroadcastMessage(string message, Socket excludeSocket)
        {
            lock (clientSockets)  // Đảm bảo an toàn thread
            {
                foreach (var socket in clientSockets)
                {
                    if (socket != excludeSocket)
                    {
                        try
                        {
                            socket.Send(Encoding.UTF8.GetBytes(message));
                        }
                        catch (Exception ex)
                        {
                            // Xử lý khi client ngắt kết nối
                            clientSockets.Remove(socket);
                            socket.Close();
                        }
                    }
                }
            }
        }

        private void UpdateChatHistoryThreadSafe(string text)
        {
            if (richTextBox1.InvokeRequired)
            {
                var d = new SafeCallDelegate(UpdateChatHistoryThreadSafe);
                richTextBox1.Invoke(d, new object[] { text });

            }
            else
            {
                richTextBox1.Text += text + "\n";
            }
        }

    }
}
