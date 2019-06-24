using System.Windows;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Windows.Controls;
using System.Text;

namespace WpfApp8
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // стандартные Ip адреса
            cbIpServer.Items.Add("0.0.0.0");
            cbIpServer.Items.Add("127.0.0.1");

            // адреса локальной машины
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                cbIpServer.Items.Add(ip.ToString());
            }
            cbIpServer.SelectedIndex = 0;
            // флаг запущен сервер или нет Start/Stop
            btnStart.Tag = false;
            // флаг останавливающий сервер
            IsStopServer = false;
        }

        Thread SrvThread;
        TcpListener SrvSocket;
        bool IsStopServer;
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if((bool)btnStart.Tag == false)
            {// запускаем сервер
                // создание сокета сервера для прослушки
                SrvSocket = new TcpListener(
                    IPAddress.Parse(cbIpServer.SelectedItem.ToString()),
                    int.Parse(txtPort.Text));
                // начало прослушивания сети по IP:port
                SrvSocket.Start(100);
                // далее должна быть команда Accept()
                SrvThread = new Thread(ServerThreadRoutine);
                //SrvThread.IsBackground = true;
                SrvThread.Start();
                btnStart.Content = "Stop";
                btnStart.Tag = true;
            }
            else
            {// останавливаем сервер

                btnStart.Content = "Start";
                btnStart.Tag = false;
            }
        }

        private void ServerThreadRoutine(object obj)
        {
            // ThreadPool.SetMaxThreads
            TcpListener srvSock = obj as TcpListener;
            // синхронный вариант сервера
            //while (true)
            //{
            //    // не асинхронный блокирующий вызов Accept
            //    // ожидание подключения удаленного клиента
            //    TcpClient client = srvSock.AcceptTcpClient();
            //    // запуск клиентского потока
            //    // работа с клиентом в отдельном потоке
            //    ThreadPool.QueueUserWorkItem(ClientThreadRoutine, client);
            //}

            // ассинхронный вариант Accept()
            while (true)
            {
                IAsyncResult ia = srvSock.BeginAcceptTcpClient(ClientThreadRoutine, srvSock);
                // ожидание завершения асинхронного вызова
                while (ia.AsyncWaitHandle.WaitOne(200) == false)
                {// WaitOne() завершился по тайм ауту
                    if (IsStopServer)
                    {
                        // завершение приема удаленных клиентов
                        //srvSock
                        return;
                    }
                }
            }
        }

        private void ClientThreadRoutine(IAsyncResult ia)
        {
            TcpListener srvSock = ia.AsyncState as TcpListener;
            TcpClient client = srvSock.EndAcceptTcpClient(ia);
            ThreadPool.QueueUserWorkItem(ClientThreadRoutine2, client);
        }

        delegate void DelegateAppendTextBox(TextBox tb, string str);
        void AppendTextBox(TextBox tb, string str)
        {
            tb.AppendText(str + "\n");
        }
        // поток работы с удаленным клиентом
        private void ClientThreadRoutine2(object obj)
        {
            TcpClient client = obj as TcpClient;
            // вывод информации в журнал о соединении
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText("Успешное соединение клиент\n");
            });
            // о клиенте - его IP и порт
            string s = "IP:port клиента: " + client.Client.RemoteEndPoint.ToString();
            Dispatcher.Invoke(
                new DelegateAppendTextBox(AppendTextBox), txtLog, s);

            // дальше по протоколу
            // 1 - ждем от клиента его имени
            byte[] buf = new byte[4 * 1024];
            int recSize = client.Client.Receive(buf);
            Dispatcher.Invoke(
                new DelegateAppendTextBox(AppendTextBox), txtLog, 
                "Имя клиента: " +
                Encoding.UTF8.GetString(buf, 0, recSize));
            // 2 - ответ сервера
            string ClientName = Encoding.UTF8.GetString(buf, 0, recSize);
            client.Client.Send(Encoding.UTF8.GetBytes("Hello " + ClientName + "!"));

            while (true)
            {
                recSize = client.Client.Receive(buf);
                Dispatcher.Invoke(
                new DelegateAppendTextBox(AppendTextBox), txtLog, 
                Encoding.UTF8.GetString(buf, 0, recSize));
                client.Client.Send(buf, recSize, SocketFlags.None);
            }
        }
    }
}
