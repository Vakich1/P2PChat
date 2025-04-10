using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatApp
{
    public class ChatServer : IDisposable
    {
        private TcpListener server;
        private List<TcpClient> clients = new List<TcpClient>();
        private HashSet<string> connectedIPs = new HashSet<string>();
        private object ipLock = new object();
        private bool isRunning;

        public event Action<string> MessageReceived;

        public void StartServer(string ip, int port)
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(ip);
                server = new TcpListener(ipAddress, port);
                server.Start();
                isRunning = true;

                new Thread(AcceptClients)
                {
                    IsBackground = true,
                    Name = "AcceptClientsThread"
                }.Start();

                MessageReceived?.Invoke($"Сервер запущен на {ip}:{port}");
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"Ошибка запуска сервера: {ex.Message}");
            }
        }

        private void AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    lock (ipLock)
                    {
                        if (connectedIPs.Contains(clientIP))
                        {
                            RejectClient(client, $"IP {clientIP} уже подключен");
                            continue;
                        }
                        connectedIPs.Add(clientIP);
                    }

                    lock (clients) clients.Add(client);

                    MessageReceived?.Invoke($"Новое подключение: {clientIP}");

                    new Thread(() => HandleClient(client))
                    {
                        IsBackground = true,
                        Name = $"ClientHandler_{clientIP}"
                    }.Start();
                }
                catch (SocketException) when (!isRunning) { break; }
                catch (Exception ex)
                {
                    MessageReceived?.Invoke($"Ошибка приема подключения: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[4096];

                while (isRunning && client.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string fullMessage = $"[{clientIP}]: {message}";

                    MessageReceived?.Invoke(fullMessage);
                    BroadcastMessage(fullMessage, client);
                }
            }
            catch (IOException) { }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"Ошибка клиента {clientIP}: {ex.Message}");
            }
            finally
            {
                lock (ipLock) connectedIPs.Remove(clientIP);
                lock (clients) clients.Remove(client);
                client.Close();
                MessageReceived?.Invoke($"Отключение: {clientIP}");
            }
        }

        private void RejectClient(TcpClient client, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes("!REJECT: " + message);
                client.GetStream().Write(data, 0, data.Length);
            }
            catch { }
            finally
            {
                client.Close();
            }
        }

        private void BroadcastMessage(string message, TcpClient sender = null)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            lock (clients)
            {
                foreach (var client in clients.ToArray())
                {
                    if (client == sender || !client.Connected) continue;

                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch { }
                }
            }
        }

        public void SendMessage(string message)
        {
            BroadcastMessage("Сервер: " + message);
        }

        public void StopServer()
        {
            isRunning = false;
            try { server?.Stop(); } catch { }

            lock (clients)
            {
                foreach (var client in clients) { try { client.Close(); } catch { } }
                clients.Clear();
            }

            lock (ipLock) connectedIPs.Clear();
        }

        public void Dispose() => StopServer();
    }
}
