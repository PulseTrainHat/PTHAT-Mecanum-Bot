﻿using System;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace PulseTrainHATMecanumBotStreamSocket
{
    internal class SocketClient
    {
        private readonly string _ip;
        private readonly int _port;
        private StreamSocket _socket;
        private DataWriter _writer;
        private DataReader _reader;

        public delegate void Error(string message);
        public event Error OnError;

        public delegate void DataRecived(string data);
        public event DataRecived OnDataRecived;

        public string Ip { get { return _ip; } }
        public int Port { get { return _port; } }

        public SocketClient(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public async void Connect()
        {
            try
            {
                var hostName = new HostName(Ip);
                _socket = new StreamSocket();
                await _socket.ConnectAsync(hostName, Port.ToString());
                _writer = new DataWriter(_socket.OutputStream);
                Read();
            }
            catch (Exception ex)
            {
                if (OnError != null)
                    OnError(ex.Message);
            }
        }

        public async void Send(string message)
        {
            //Envia o tamanho da string
            _writer.WriteUInt32(_writer.MeasureString(message));
            //Envia a string em si
            _writer.WriteString(message);

            try
            {
                //Faz o Envio da mensagem
                await _writer.StoreAsync();
                //Limpa para o proximo envio de mensagem
                await _writer.FlushAsync();
            }
            catch (Exception ex)
            {
                if (OnError != null)
                    OnError(ex.Message);
            }
        }

        private async void Read()
        {
            _reader = new DataReader(_socket.InputStream);
            try
            {
                while (true)
                {
                    uint sizeFieldCount = await _reader.LoadAsync(sizeof(uint));
                    //if desconneted
                    if (sizeFieldCount != sizeof(uint))
                        return;

                    uint stringLenght = _reader.ReadUInt32();
                    uint actualStringLength = await _reader.LoadAsync(stringLenght);
                    //if desconneted
                    if (stringLenght != actualStringLength)
                        return;
                    if (OnDataRecived != null)
                        OnDataRecived(_reader.ReadString(actualStringLength));
                }

            }
            catch (Exception ex)
            {
                if (OnError != null)
                    OnError(ex.Message);
            }
        }

        public void Close()
        {
            _writer.DetachStream();
            _writer.Dispose();

            _reader.DetachStream();
            _reader.Dispose();

            _socket.Dispose();
        }
    }
}
