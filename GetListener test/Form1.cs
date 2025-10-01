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
using System.Threading;

namespace GetListener_test
{
    public partial class Form1 : Form
    {
        ListenFormServer server;
        SandMoxaDo sandtoMoxa;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            server = new ListenFormServer(this);   
            server.Start(5000); // 啟動 Server，監聽 5000 port

            sandtoMoxa = new SandMoxaDo(this, "127.0.0.1", 5001);
            sandtoMoxa.Start();
        }

        private void Form1_FormClosing_1(object sender, FormClosingEventArgs e)
        {
            server.Stop();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sandtoMoxa.SendDoCommand(0,1);
        }
    }

    internal class ListenFormServer
    {
        private TcpListener listener;
        private bool isRunning = false;
        private Form1 _form;

        public ListenFormServer(Form1 form)
        {
            _form = form;
        }

        public void Start(int port = 5000)
        {
            if (isRunning) return;

            isRunning = true;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Console.WriteLine($"Server 已啟動，監聽 Port {port} (等待換版訊號)");

            Task.Run(ListenLoop);
        }

        private async void ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client));
                }
                catch (ObjectDisposedException)
                {
                    // listener 已停止
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"接受 Client 失敗: {ex.Message}");
                }
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[4]; // int = 4 bytes

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // client 斷線

                    int number = BitConverter.ToInt32(buffer, 0);

                    // 更新 UI
                    _form.Invoke((MethodInvoker)(() =>
                    {
                        _form.listBox1.Items.Insert(0, $"收到數字: {number}");
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"處理 Client 錯誤: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("Client 離線");
            }
        }


        public void Stop()
        {
            isRunning = false;
            listener?.Stop();
            Console.WriteLine("Server 已停止");
        }
    }


    public class SandMoxaDo
    {
        private TcpClient client;
        private NetworkStream stream;
        private string serverIp;
        private int serverPort;
        private bool running = false;
        private Form1 _form;

        public SandMoxaDo(Form1 form, string ip, int port)
        {
            _form = form;
            serverIp = ip;
            serverPort = port;
        }

        public void Start()
        {
            running = true;
            _ = Task.Run(ConnectLoop);
        }

        private async Task ConnectLoop()
        {
            while (running)
            {
                try
                {
                    if (client == null || !client.Connected)
                    {
                        client = new TcpClient();
                        await client.ConnectAsync(serverIp, serverPort);
                        stream = client.GetStream();
                        messgaeShow($"✅ 已連線 {serverIp}:{serverPort}");
                    }
                }
                catch
                {
                    messgaeShow($"❌ 連線失敗，3 秒後重試...");
                    await Task.Delay(3000);
                }

                await Task.Delay(1000); // 每秒檢查一次狀態
            }
        }

        public async Task SendDoCommand(int doNumber, int onOff)
        {
            try
            {
                if (client == null || !client.Connected)
                {
                    messgaeShow("⚠️ 尚未連線，嘗試重新連線...");
                    await Reconnect();
                    if (client == null || !client.Connected) return;
                }

                byte[] data = new byte[8];
                Array.Copy(BitConverter.GetBytes(doNumber), 0, data, 0, 4);
                Array.Copy(BitConverter.GetBytes(onOff), 0, data, 4, 4);

                await stream.WriteAsync(data, 0, data.Length);
                messgaeShow($"📤 已發送 DO{doNumber}, OnOff={onOff}");
            }
            catch (Exception ex)
            {
                messgaeShow($"❌ 傳送失敗：{ex.Message}");
            }
        }

        private async Task Reconnect()
        {
            try
            {
                client?.Close();
                client = new TcpClient();
                await client.ConnectAsync(serverIp, serverPort);
                stream = client.GetStream();
                messgaeShow($"🔄 已重新連線 {serverIp}:{serverPort}");
            }
            catch
            {
                messgaeShow("⚠️ 重連失敗");
            }
        }


        private void messgaeShow(string msg)
        {
            _form.Invoke((MethodInvoker)delegate
            {
                _form.listBox1.Items.Insert(0, msg);
            });
        }



        #region 三色燈
        public void redLight_on() => SendDoCommand(0, 1);
        public void yellowLight_on() => SendDoCommand(1, 1);
        public void greenLight_on() => SendDoCommand(2, 1);
        public void buzzer_on() => SendDoCommand(3, 1);
        public void Do4_on() => SendDoCommand(4, 1);

        public void redLight_off() => SendDoCommand(0, 0);
        public void yellowLight_off() => SendDoCommand(1, 0);
        public void greenLight_off() => SendDoCommand(2, 0);
        public void buzzer_off() => SendDoCommand(3, 0);
        public void Do4_off() => SendDoCommand(4, 0);

        public void turnOffAllDo()
        {
            SendDoCommand(0, 0);
            SendDoCommand(1, 0);
            SendDoCommand(2, 0);
            SendDoCommand(3, 0);
            SendDoCommand(4, 0);
        }

        #endregion


        public void Close()
        {
            stream?.Close();
            client?.Close();
        }
    }

}
