using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace ChatApp
{
    public partial class ChatForm : Form
    {
        private TextBox txtServerIP, txtServerPort, txtClientIP, txtMessage;
        private Button btnStartServer, btnConnect, btnSend;
        private ListBox lstMessages;
        private ChatServer server;
        private ChatClient client;

        public ChatForm()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void InitializeComponent()
        {
            this.Text = "Сетевой Чат";
            this.Size = new System.Drawing.Size(600, 500);
            this.MinimumSize = new System.Drawing.Size(600, 500);
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new System.Drawing.Font("Segoe UI", 9F);

            // Цвета
            var primaryColor = System.Drawing.Color.FromArgb(33, 150, 243); // Синий
            var accentColor = System.Drawing.Color.FromArgb(76, 175, 80);   // Зелёный
            var textColor = System.Drawing.Color.White;
            var inputColor = System.Drawing.Color.White;

            // Блок сетевых настроек (сверху, в одну строку)
            new Label { Text = "Сервер:", Left = 10, Top = 12, Width = 55 }.AddTo(this);
            txtServerIP = new TextBox { Left = 65, Top = 10, Width = 100, Text = "127.0.0.1", BackColor = inputColor }.AddTo(this);
            txtServerPort = new TextBox { Left = 170, Top = 10, Width = 50, Text = "5000", BackColor = inputColor }.AddTo(this);
            btnStartServer = new Button
            {
                Left = 230,
                Top = 8,
                Width = 130,
                Height = 26,
                Text = "Старт сервера",
                BackColor = primaryColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            }.AddTo(this);

            new Label { Text = "Ваш IP:", Left = 370, Top = 12, Width = 50 }.AddTo(this);
            txtClientIP = new TextBox { Left = 425, Top = 10, Width = 100, Text = "192.168.0.100", BackColor = inputColor }.AddTo(this);
            btnConnect = new Button
            {
                Left = 530,
                Top = 8,
                Width = 50,
                Height = 26,
                Text = "🔗",
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            }.AddTo(this);

            // Список сообщений (по центру, занимает большую часть)
            lstMessages = new ListBox
            {
                Left = 10,
                Top = 45,
                Width = 570,
                Height = 350,
                BackColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                HorizontalScrollbar = true
            }.AddTo(this);

            // Панель ввода (снизу, во всю ширину)
            txtMessage = new TextBox
            {
                Left = 10,
                Top = 410,
                Width = 470,
                Height = 34,
                BackColor = inputColor,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle
            }.AddTo(this);

            btnSend = new Button
            {
                Left = 490,
                Top = 410,
                Width = 90,
                Height = 34,
                Text = "➤",
                Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                BackColor = primaryColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            }.AddTo(this);
        }



        private void SetupEventHandlers()
        {
            btnStartServer.Click += (s, e) => StartServer();
            btnConnect.Click += (s, e) => ConnectToServer();
            btnSend.Click += (s, e) => SendMessage();
        }

        private void StartServer()
        {
            try
            {
                if (server != null)
                {
                    MessageBox.Show("Сервер уже запущен!");
                    return;
                }

                if (!int.TryParse(txtServerPort.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Неверный порт!");
                    return;
                }

                server = new ChatServer();
                server.MessageReceived += (msg) => Invoke(new Action(() => lstMessages.Items.Add(msg)));
                server.StartServer(txtServerIP.Text, port);

                lstMessages.Items.Add($"Сервер запущен на {txtServerIP.Text}:{port}");
                btnStartServer.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска сервера: {ex.Message}");
                server = null;
            }
        }

        private void ConnectToServer()
        {
            if (client?.IsConnected == true)
            {
                MessageBox.Show("Уже подключено!");
                return;
            }

            string clientIP = txtClientIP.Text.Trim();
            if (string.IsNullOrEmpty(clientIP) || !IPAddress.TryParse(clientIP, out _))
            {
                MessageBox.Show("Укажите корректный IP-адрес!");
                return;
            }

            try
            {
                client = new ChatClient(clientIP);
                client.MessageReceived += msg =>
                {
                    this.Invoke((Action)(() =>
                    {
                        if (msg.StartsWith("!REJECT:"))
                            MessageBox.Show(msg.Substring(8), "Ошибка");
                        else
                            lstMessages.Items.Add(msg);
                    }));
                };

                // Асинхронное подключение без блокировки UI
                new Thread(() => {
                    try
                    {
                        client.Connect(txtServerIP.Text, int.Parse(txtServerPort.Text));
                        this.Invoke((Action)(() =>
                            MessageBox.Show($"Успешно подключено с IP: {clientIP}")));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((Action)(() =>
                            MessageBox.Show(ex.Message, "Ошибка подключения")));
                        client?.Dispose();
                        client = null;
                    }
                }).Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
                client = null;
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                MessageBox.Show("Сообщение не может быть пустым!");
                return;
            }

            try
            {
                string message = txtMessage.Text;

                if (client != null && client.IsConnected)
                {
                    client.SendMessage(message);
                    lstMessages.Items.Add($"Вы: {message}");
                }
                else if (server != null)
                {
                    server.SendMessage(message);
                    lstMessages.Items.Add($"Сервер: {message}");
                }
                else
                {
                    MessageBox.Show("Нет активного подключения!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}");
            }
            finally
            {
                txtMessage.Clear();
            }
        }

        private void OnFormClosing()
        {
            server?.StopServer();
            client?.Disconnect();
        }
    }

    public static class ControlExtensions
    {
        public static T AddTo<T>(this T control, Control parent) where T : Control
        {
            parent.Controls.Add(control);
            return control;
        }
    }
}