using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ElementWar.Net
{
    /// <summary>UDP 客户端 socket 封装：连接 / 发送 / 非阻塞接收。</summary>
    public class UdpSocket : IDisposable
    {
        private readonly UdpClient _udp;
        private IPEndPoint _remote;

        public UdpSocket(string ip, int port)
        {
            _udp = new UdpClient();
            _remote = new IPEndPoint(IPAddress.Parse(ip), port);
            _udp.Connect(_remote);
        }

        public void SendJson(string json)
        {
            if (_udp == null) return;
            byte[] data = Encoding.UTF8.GetBytes(json);
            _udp.Send(data, data.Length);
        }

        /// <summary>非阻塞接收一条（无数据返回 null）。</summary>
        public string TryReceive()
        {
            if (_udp == null || _udp.Available <= 0) return null;
            try
            {
                byte[] data = _udp.Receive(ref _remote);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception) { return null; }
        }

        public void Dispose() { try { _udp?.Close(); } catch { } }
    }
}
