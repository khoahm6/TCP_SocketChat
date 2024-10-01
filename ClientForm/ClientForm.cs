using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.AxHost;

namespace ClientForm
{
    public partial class ClientForm : Form
    {
        private bool started = false;
        private static int _buff_size = 2048;
        private byte[] _buffer = new byte[_buff_size];
        private Socket clientSocket = null;
        private delegate void SafeCallDelegate(string text);
        public ClientForm()
        {
            InitializeComponent();
            clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress serverIp = IPAddress.Parse(textBox1.Text);
                int serverPort = int.Parse(textBox2.Text);
                IPEndPoint serverEp = new IPEndPoint(serverIp, serverPort);
                clientSocket.Connect(serverEp);
                richTextBox1.Text += "Connected to " + serverEp.ToString();

                Thread receiveThread = new Thread(ReceiveData);
                receiveThread.IsBackground = true; // Ensure the thread exits when the main program does
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] messageData = Encoding.UTF8.GetBytes(richTextBox2.Text);
                await clientSocket.SendAsync(new ArraySegment<byte>(messageData), SocketFlags.None);
                richTextBox2.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void ReceiveData()
        {
            while (true)
            {
                try
                {
                    int receivedBytes = clientSocket.Receive(_buffer);
                    string message = Encoding.UTF8.GetString(_buffer, 0, receivedBytes);

                    if (message.StartsWith("FILE:"))
                    {
                        string fileName = message.Substring(5);
                        UpdateChatHistoryThreadSafe("Receiving file: " + fileName);

                        // Nhận kích thước file
                        byte[] fileSizeBuffer = new byte[4];
                        clientSocket.Receive(fileSizeBuffer);
                        int fileSize = BitConverter.ToInt32(fileSizeBuffer, 0);

                        // Nhận dữ liệu file
                        byte[] fileData = new byte[fileSize];
                        int totalReceived = 0;

                        while (totalReceived < fileSize)
                        {
                            int received = clientSocket.Receive(fileData, totalReceived, fileSize - totalReceived, SocketFlags.None);
                            totalReceived += received;
                        }

                        // Lưu file vào máy
                        string savePath = Path.Combine("Downloads", fileName);
                        File.WriteAllBytes(savePath, fileData);

                        UpdateChatHistoryThreadSafe($"File {fileName} received and saved to {savePath}");
                    }
                    else
                    {
                        // If it's a normal message, display it
                        UpdateChatHistoryThreadSafe(message);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    break;
                }
            }
        }
        private void AppendDownloadLink(string fileName, string filePath)
        {
            // Thêm tên file vào richTextBox như một đường dẫn
            richTextBox1.AppendText("File received: " + fileName + "\n");

            // Xử lý sự kiện click vào tên file để tải file về
            richTextBox1.LinkClicked += (sender, e) =>
            {
                if (e.LinkText == fileName)
                {
                    // Mở file đã lưu
                    System.Diagnostics.Process.Start(filePath);
                }
            };
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
                richTextBox1.AppendText( text + "\n");
            }
        }
        private async void SendFile(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                string fileName = fileInfo.Name;
                byte[] fileNameData = Encoding.UTF8.GetBytes("FILE:" + fileName);
                await clientSocket.SendAsync(new ArraySegment<byte>(fileNameData), SocketFlags.None); // Notify about file

                byte[] fileSizeBuffer = BitConverter.GetBytes((int)fileInfo.Length);
                await clientSocket.SendAsync(new ArraySegment<byte>(fileSizeBuffer), SocketFlags.None); // Send file size

                byte[] fileData = File.ReadAllBytes(filePath);
                await clientSocket.SendAsync(new ArraySegment<byte>(fileData), SocketFlags.None); // Send file data

                UpdateChatHistoryThreadSafe($"File sent: {fileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void btnSendFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    SendFile(filePath);
                }
            }
        }
    }
}
