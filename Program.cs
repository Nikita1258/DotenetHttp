using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetHttp
{
    class Program
    {
        static List<ContextHandler> handlers = new List<ContextHandler>();
        static Message[] messages = new Message[5];
        static void Main(string[] args)
        {
            handlers.Add(Root);
            handlers.Add(GetList);
            handlers.Add(AppendList);
            handlers.Add(NotFound);

            HttpListener server = new HttpListener();
            server.Prefixes.Add("http://localhost:8888/");
            server.Prefixes.Add("http://127.0.0.1:8888/");
            server.Start();
            while(true)
            {
                HttpListenerContext ctx = server.GetContext();
                Task.Run(() => 
                {
                    try
                    {
                        int i = 0;
                        bool result;
                        do
                            result = handlers[i++](ctx.Request, ctx.Response);
                        while(!result);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                        ctx.Response.StatusCode = 500;
                        ctx.Response.Close();
                    }
                });
            }
        }

        static bool Root(HttpListenerRequest req, HttpListenerResponse res)
        {
            if(req.RawUrl != "/")
                return false;

            res.ContentType = "text/html";
            res.AddHeader("Charset", "UTF-8");
            BinaryReader reader = new BinaryReader(new FileStream("index.html", FileMode.Open), Encoding.UTF8);
            byte[] buffer = new byte[reader.BaseStream.Length];
            reader.Read(buffer);
            res.OutputStream.Write(buffer);
            res.StatusCode = 200;
            res.Close();
            reader.Close();
            return true;
        }

        static bool GetList(HttpListenerRequest req, HttpListenerResponse res)
        {
            if(req.RawUrl != "/list" || req.HttpMethod != "GET")
                return false;

            res.ContentType = "application/json";
            res.AddHeader("Charset", "UTF-8");
            int ind = Array.IndexOf(messages, null);
            Message[] temp = new Message[ind == -1 ? messages.Length : ind];
            Array.Copy(messages, temp, temp.Length);
            string str = JsonSerializer.Serialize(temp, typeof(Message[]));
            res.OutputStream.Write(Encoding.UTF8.GetBytes(str));
            res.StatusCode = 200;
            res.Close();
            return true;
        }

        static bool AppendList(HttpListenerRequest req, HttpListenerResponse res)
        {
            if(req.RawUrl != "/list" || req.HttpMethod != "POST")
                return false;

            if(req.ContentType != "application/json")
            {
                res.StatusCode = 400;
                res.Close();
                return true;
            }

            byte[] buffer = new byte[req.ContentLength64];
            for(int i = 0; ; i++)
            {
                int t = req.InputStream.ReadByte();
                if (t == -1)
                    break;
                buffer[i] = (byte)t;
            }

            string s = Encoding.UTF8.GetString(buffer);
            var temp = JsonSerializer.Deserialize<Message>(s);
            if(temp.author == "" || temp.message == "")
            {
                res.StatusCode = 400;
                res.Close();
                return true;
            }
            temp.dateTime = DateTime.Now.ToString("dd.MM.yy HH:mm:ss");

            for(int i = messages.Length - 1; i > 0; i--)
                messages[i] = messages[i - 1];
            messages[0] = temp;

            res.ContentType = "application/json";
            res.AddHeader("Charset", "UTF-8");
            string str = JsonSerializer.Serialize(messages[0]);
            res.OutputStream.Write(Encoding.UTF8.GetBytes(str));
            res.StatusCode = 202;
            res.Close();
            return true;
        }

        static bool NotFound(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.StatusCode = 404;
            res.Close();
            return true;
        }
    }

    class Message
    {
        public string author{ get; set; }
        public string message{ get; set; }
        public string dateTime{ get; set; }
    }
    delegate bool ContextHandler(HttpListenerRequest req, HttpListenerResponse res);
}
