using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using database;

class Program
{
    static async Task Main()
    {
        // --- トークン設定 ---
        string appToken = Environment.GetEnvironmentVariable("SLACK_APP_TOKEN")?.Trim()
            ?? throw new Exception("SLACK_APP_TOKEN が未設定");
        string botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")?.Trim()
            ?? throw new Exception("SLACK_BOT_TOKEN が未設定");

        Console.WriteLine("Fetching WebSocket URL for Socket Mode...");

        // 1. Socket Mode URLを取得
        string socketUrl = await GetSocketModeUrl(appToken);
        Console.WriteLine("WebSocket URL obtained.");

        using var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(new Uri(socketUrl), CancellationToken.None);
            Console.WriteLine("Connected to Slack Socket Mode!");
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine("WebSocket connection failed: " + ex.Message);
            return;
        }

        // データベース初期化
        database_command dbcmd = new database_command();
        dbcmd.databaseinit();
        Console.WriteLine("Database initialized.");


        // メッセージ受信用バッファ
        var buffer = new byte[1024 * 4];

        // 2. メッセージ受信ループ
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine("WebSocket receive error: " + ex.Message);
                break;
            }

            if (result.Count == 0) continue;

            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine("Received: " + message);

            // JSONパース
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // json 全体表示（デバッグ用）
                Console.WriteLine(root.ToString());
                Console.WriteLine("-----");

                // Events API のメッセージのみ処理
                if (root.TryGetProperty("type", out var typeElem) &&
                    typeElem.GetString() == "events_api")
                {
                    //Console.WriteLine("Events API message received.");

                    var payload = root.GetProperty("payload");
                    var eventElem = payload.GetProperty("event");

                    if (eventElem.TryGetProperty("channel", out var channelElem) &&
                        !string.IsNullOrEmpty(channelElem.GetString()))
                    {
                        string channel = channelElem.GetString()!;
                        string text = root.GetProperty("payload").GetProperty("event").GetProperty("text").GetString()!;
                        //string text = "Hello from C# Socket Mode Bot!";

                        // メッセージ送信
                        await SendMessage(botToken, channel, text);
                    }

                    // SlackにACKを返す
                    var envelopeId = root.GetProperty("envelope_id").GetString();
                    var ackJson = JsonSerializer.Serialize(new { envelope_id = envelopeId });
                    var ackBytes = Encoding.UTF8.GetBytes(ackJson);
                    await ws.SendAsync(ackBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine("JSON parse error: " + ex.Message);
            }
        }
    }

    // --- Socket Mode URL取得 ---
    static async Task<string> GetSocketModeUrl(string appToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", appToken);

        var response = await client.PostAsync("https://slack.com/api/apps.connections.open", null);
        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.GetProperty("ok").GetBoolean())
        {
            return root.GetProperty("url").GetString()!;
        }
        else
        {
            throw new Exception("Failed to get Socket Mode URL: " + json);
        }
    }

    // --- メッセージ送信 ---
    static async Task SendMessage(string botToken, string channel, string text)
    {
        if (string.IsNullOrEmpty(channel) || string.IsNullOrEmpty(text)) return;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", botToken);

        var payload = new { channel = channel, text = text };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://slack.com/api/chat.postMessage", content);
        string responseText = await response.Content.ReadAsStringAsync();
        Console.WriteLine("Message send response: " + responseText);
    }
}
