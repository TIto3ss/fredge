using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;



using database; // データベース用の名前空間

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
                //Console.WriteLine(root.ToString());
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

                        

                        // textを見てそれに応じたメッセージを返す
                        string text = await TextAnalyze(root);

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

    // テキストとユーザIDを取り出して解析する関数
    static async Task<string> TextAnalyze(JsonElement root)
    {   
        database_command dbcmd = new database_command();
        
        string userId = root.GetProperty("payload")
            .GetProperty("event")
            .GetProperty("user")
            .GetString()!;
        string text = root.GetProperty("payload")
            .GetProperty("event")
            .GetProperty("text")
            .GetString()!;
       


        // try
        // {
        //     // メッセージが画像のみの場合、elements配列が1つしかないため例外が発生する
        //     if (target.GetArrayLength() < 2)
        //     {
        //         return "内容がNULLです。";
        //     }
        // }
        // catch (Exception)
        // {
        //     return "テキストが含まれていません。";
        // }
        
        

        // ラインナップ確認
        if (Regex.Match(text, @"ラインナップ").Success)
        {   
            string lineup = dbcmd.item_check_command();
            return lineup;
        }
        // 購入
        else if (Regex.Match(text, @"購入").Success)
        {   
            
            string username;
            // もし商品テーブルに商品名がなければキャンセル
            string item = Regex.Replace(text, @"^.*購入[：:]", "").Trim();
            bool itemExists = dbcmd.data_existence_check_command("items", "item_name", item);

            if (!itemExists)
            {
                // ラインナップを表示
                string lineup = dbcmd.item_check_command();
                return "商品が存在しません。ラインナップはこちら。\n" + lineup;

            }
            
            // もしユーザテーブルにユーザIDがなければ追加
            bool userExists = dbcmd.data_existence_check_command("users", "user_id", userId);
            if (!userExists)
            {   
                string botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")?.Trim()
                    ?? throw new Exception("SLACK_BOT_TOKEN が未設定");
                username = await GetSlackUserNameAsync(botToken, userId);
                //Console.WriteLine($"Message from {username} in channel {channel}");
                dbcmd.user_register_command(userId, username);
            }
            
            // データベースにあるユーザIDからユーザ名を取得
            username = dbcmd.get_user_name_command(userId);

            dbcmd.purchase_command(item, 1, userId);

            return $"{username}さんが{item}を購入しました。";
        }
        // 入金
        else if (Regex.Match(text, @"入金", RegexOptions.IgnoreCase).Success)
        {   
            // もしユーザテーブルにユーザIDがなければ追加
            bool userExists = dbcmd.data_existence_check_command("users", "user_id", userId);
            string username;

            if (!userExists)
            {   
                string botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")?.Trim()
                    ?? throw new Exception("SLACK_BOT_TOKEN が未設定");
                username = await GetSlackUserNameAsync(botToken, userId);
                //Console.WriteLine($"Message from {username} in channel {channel}");
                dbcmd.user_register_command(userId, username);
            }

            // データベースにあるユーザIDからユーザ名を取得
            username = dbcmd.get_user_name_command(userId);
            // 入金額を取得
            string amountStr = Regex.Replace(text, @"^.*入金[：:]", "").Trim();
            // 全角数字を半角に変換
            amountStr = amountStr.Normalize(NormalizationForm.FormKC);
            

            if (!int.TryParse(amountStr, out int amount) || amount <= 0 || amount > 10000)
            {
                return "正しい入金額を入力してください。例: 入金:1000";
            }
            dbcmd.offering_box_command(userId, amount);

            return $"{username}さんが{amount}円入金しました。";
        }
        // 入金差額確認
        else if (Regex.Match(text, @"差額確認").Success)
        {   
            string user_name = dbcmd.get_user_name_command(userId);
            int amount = dbcmd.balance_calculation_command(user_name);
            dbcmd.table_check_command("offering_box");
            
            return $"{amount}円の入金差額です（不足時はマイナス）。";


        }

        // ーーーーーーーーーーーーー管理者機能ーーーーーーーーーーーーーーーー

        // 補填
        else if (Regex.Match(text, @"補填").Success)
        {   
            // 補填額を取得
            string text_hoten = Regex.Replace(text, @"^.*補填[：:]", "").Trim();

            Regex regex = new Regex(@"^(.+)[-ー](\d+).?$");
            Match match = regex.Match(text_hoten);
            if (!match.Success)
            {
                return "正しい補填形式を入力してください。例: 補填:商品名-1000";
            }

            string itemname = match.Groups[1].Value;
            // 全角数字を半角に変換
            string amountStr = match.Groups[2].Value;
            amountStr = amountStr.Normalize(NormalizationForm.FormKC);
            int amount = int.Parse(amountStr);
            
            dbcmd.restock_command(itemname, amount, userId);

            if (dbcmd.data_existence_check_command("items", "item_name", itemname) == false)
            {   
                string lineup = dbcmd.item_check_command();
                return "その商品は存在しません。" + lineup;
            }

            // もしユーザテーブルにユーザIDがなければ追加
            bool userExists = dbcmd.data_existence_check_command("users", "user_id", userId);
            if (!userExists)
            {   
                string botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")?.Trim()
                    ?? throw new Exception("SLACK_BOT_TOKEN が未設定");
                string username = await GetSlackUserNameAsync(botToken, userId);
                //Console.WriteLine($"Message from {username} in channel {channel}");
                dbcmd.user_register_command(userId, username);
            }


            string user_name = dbcmd.get_user_name_command(userId);

            return $"{user_name}が{itemname}補填を{amount}個補填しました。";
        }

        // 台帳確認(全員分の入金差額を表示)
        else if (Regex.Match(text, @"台帳確認").Success)
        {
            string ledger = dbcmd.all_user_balance_command();

            return ledger;

        }

        // 在庫確認
        else if (Regex.Match(text, @"在庫確認").Success)
        {
            string itemname = Regex.Replace(text, @"^.*在庫確認[：:]", "").Trim();
            int stock = dbcmd.stock_calculation_command(itemname);

            return $"{itemname}の在庫数は{stock}です。";
        }

        // 商品登録
        else if (Regex.Match(text, @"商品登録").Success)
        {   
            // 商品名と価格を取得
            string textitem = Regex.Replace(text, @"^.*商品登録[：:]", "").Trim();

            Regex regex = new Regex(@"^(.+)[-ー](\d+).?$");
            Match match = regex.Match(textitem);
            if (!match.Success)
            {
                return "正しい商品登録形式を入力してください。例: 商品登録:商品名-価格";
            }

            string itemname = match.Groups[1].Value;
            // 全角数字を半角に変換
            string priceStr = match.Groups[2].Value;
            priceStr = priceStr.Normalize(NormalizationForm.FormKC);
            int price = int.Parse(priceStr);


            dbcmd.item_register_command(itemname, price);

            return $"{itemname}を価格{price}円で登録しました。";
        }

        // 商品削除
        else if (Regex.Match(text, @"商品削除").Success)
        {   
            // 商品名を取得
            string itemname = Regex.Replace(text, @"^.*商品削除[：:]", "").Trim();
            dbcmd.item_delete_command(itemname);

            return $"{itemname}を商品リストから削除しました。";
        }


        // 使い方を返信
        else
        {
            return "説明している時間がない";
        }
    }

    // ユーザIDからユーザ
    // 名を取得する関数
    static async Task<string> GetSlackUserNameAsync(string token, string userId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetStringAsync(
            $"https://slack.com/api/users.info?user={userId}");

        using var doc = JsonDocument.Parse(response);

        Console.WriteLine("User info response: " + doc.RootElement.ToString());

        return doc.RootElement
            .GetProperty("user")
            .GetProperty("profile")
            .GetProperty("display_name")
            .GetString();
    }
}

