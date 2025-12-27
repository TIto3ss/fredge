using System;
using Npgsql;

namespace database
{
    class database_command
    {       
        static string connectionString = "Host=localhost;Username=postgres;Password=test;Database=postgres";
        static string newconnectionString = "Host=localhost;Username=postgres;Password=test;Database=backyard";
        static string databaseName = "backyard";

        public void databaseinit(){
            
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    //Console.WriteLine("接続成功");
                    
                    var checkCmd = new NpgsqlCommand(
                        $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'", conn);
                    var exists = checkCmd.ExecuteScalar();

                    if (exists == null)
                    {
                        // データベース作成
                        using (var cmd = new NpgsqlCommand($"CREATE DATABASE {databaseName}", conn))
                        {
                            cmd.ExecuteNonQuery();
                            Console.WriteLine($"データベース '{databaseName}' 作成成功");
                        } 
                    }
                    else
                    {
                        Console.WriteLine($"データベース '{databaseName}' は既に存在します");
                    }    
                }

                // 新しいデータベースに接続してテーブルを作成
                using (var conn = new NpgsqlConnection(newconnectionString))
                {
                    conn.Open();
                    //Console.WriteLine("接続成功");

                    // 商品テーブル作成
                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS items (
                            item_name TEXT NOT NULL PRIMARY KEY,
                            price INT NOT NULL
                        )";

                    using (var cmd = new NpgsqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("テーブル 'items' 作成成功");
                    }

                    // ユーザーテーブル作成
                    createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS users (
                            user_id TEXT NOT NULL PRIMARY KEY,
                            user_name TEXT NOT NULL UNIQUE
                        )";
                    using (var cmd = new NpgsqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("テーブル 'users' 作成成功");
                    }

                    // 購入履歴テーブル作成
                    createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS purchase_history (
                            purchase_id SERIAL PRIMARY KEY,
                            item_name TEXT NOT NULL,
                            num INT NOT NULL,
                            user_id TEXT NOT NULL,
                            purchase_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var cmd = new NpgsqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("テーブル 'purchase_history' 作成成功");
                    }

                    // 補填履歴テーブル作成
                    createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS restock_history (
                            restock_id SERIAL PRIMARY KEY,
                            item_name TEXT NOT NULL,
                            num INT NOT NULL,
                            user_id TEXT NOT NULL,
                            restock_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var cmd = new NpgsqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("テーブル 'restock_history' 作成成功");
                    }

                    // 台帳テーブル作成
                    createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS offering_box (
                            payment_id SERIAL PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            amount INT NOT NULL,
                            change_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP                            
                        )";
                    using (var cmd = new NpgsqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("テーブル 'offering_box' 作成成功");
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                return;
            }
        }


        // 商品登録コマンド
        public void item_register_command(string item_name, int price)
        {
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();

                    string insertQuery = @"
                        INSERT INTO items (item_name, price)
                        VALUES (@item_name, @price)";

                    using (var cmd = new NpgsqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@item_name", item_name);
                        cmd.Parameters.AddWithValue("@price", price);
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("商品登録成功");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        // 商品削除コマンド
        public void item_delete_command(string item_name)
        {
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();

                    string deleteQuery = @"
                        DELETE FROM items
                        WHERE item_name = @item_name";

                    using (var cmd = new NpgsqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@item_name", item_name);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine("商品削除成功");
                        }
                        else
                        {
                            Console.WriteLine("指定された商品が見つかりません");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }


        // ユーザー登録コマンド
        public void user_register_command(string user_id, string user_name)
        {
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();

                    string insertQuery = @"
                        INSERT INTO users (user_id, user_name)
                        VALUES (@user_id, @user_name)";

                    using (var cmd = new NpgsqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@user_id", user_id);
                        cmd.Parameters.AddWithValue("@user_name", user_name);
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("ユーザー登録成功");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        // ユーザー削除コマンド
        public void user_delete_command(string user_id)
        {
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();

                    string deleteQuery = @"
                        DELETE FROM users
                        WHERE user_id = @user_id";

                    using (var cmd = new NpgsqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@user_id", user_id);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine("ユーザー削除成功");
                        }
                        else
                        {
                            Console.WriteLine("指定されたユーザーが見つかりません");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        // 購入コマンド
        public void purchase_command(string item_name, int num, string user_id)
        {
            using (var conn = new NpgsqlConnection(newconnectionString)){
                conn.Open();
                string purchase_Query = @"
                    INSERT INTO purchase_history (item_name, num, user_id)
                    VALUES (@item_name, @num, @user_id)";

                using (var cmd = new NpgsqlCommand(purchase_Query, conn))
                {
                    cmd.Parameters.AddWithValue("@item_name", item_name);
                    cmd.Parameters.AddWithValue("@num", num);
                    cmd.Parameters.AddWithValue("@user_id", user_id);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("購入記録成功");
            }
        }
        
        // 補填コマンド
        public void restock_command(string item_name, int num, string user_id)
        {
            using (var conn = new NpgsqlConnection(newconnectionString)){
                conn.Open();
                string restock_Query = @"
                    INSERT INTO restock_history (item_name, num, user_id)
                    VALUES (@item_name, @num, @user_id)";

                using (var cmd = new NpgsqlCommand(restock_Query, conn))
                {
                    cmd.Parameters.AddWithValue("@item_name", item_name);
                    cmd.Parameters.AddWithValue("@num", num);
                    cmd.Parameters.AddWithValue("@user_id", user_id);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("補填記録成功");
            }
        }

        // 入金コマンド
        public void offering_box_command(string user_id, int amount)
        {
            using (var conn = new NpgsqlConnection(newconnectionString)){
                conn.Open();
                string offering_box_Query = @"
                    INSERT INTO offering_box (user_id, amount)
                    VALUES (@user_id, @amount)";

                using (var cmd = new NpgsqlCommand(offering_box_Query, conn))
                {
                    cmd.Parameters.AddWithValue("@user_id", user_id);
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("入金記録成功");
            }
        }

        // 購入数計算コマンド
        public int purchase_sum_command(string item_name)
        {
            int total = 0;
            using (var conn = new NpgsqlConnection(newconnectionString)){
                conn.Open();
                string sum_Query = @"
                    SELECT SUM(num) FROM purchase_history
                    WHERE item_id = @item_id";

                using (var cmd = new NpgsqlCommand(sum_Query, conn))
                {
                    cmd.Parameters.AddWithValue("@item_id", item_name);
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        total = Convert.ToInt32(result);
                    }
                }
            }
            return total;
        }

        // 補填数計算コマンド
        public int restock_sum_command(string item_name)
        {
            int total = 0;
            using (var conn = new NpgsqlConnection(newconnectionString)){
                conn.Open();
                string sum_Query = @"
                    SELECT SUM(num) FROM restock_history
                    WHERE item_id = @item_id";

                using (var cmd = new NpgsqlCommand(sum_Query, conn))
                {
                    cmd.Parameters.AddWithValue("@item_id", item_name);
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        total = Convert.ToInt32(result);
                    }
                }
            }
            return total;
        }
          
        // 在庫計算コマンド
        public int stock_calculation_command(string item_name)
        {
            int purchase_total = purchase_sum_command(item_name);
            int restock_total = restock_sum_command(item_name);
            int stock = restock_total - purchase_total;
            return stock;
        }

        // 購入金額計算コマンド
        public int purchase_price_command(string user_name)
        {
            int total_price = 0;
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();
                    string price_Query = @"
                        SELECT SUM(i.price * p.num) FROM purchase_history p
                        JOIN items i ON p.item_id = i.item_name
                        JOIN users u ON p.usetr_id = u.user_id
                        WHERE u.user_name = @user_name";

                    using (var cmd = new NpgsqlCommand(price_Query, conn))
                    {
                        cmd.Parameters.AddWithValue("@user_name", user_name);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value)
                        {
                            total_price = Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
            return total_price;
        }

        // 入金金額計算コマンド
        public int offering_box_price_command(string user_name)
        {
            int total_amount = 0;
            try
            {
                using (var conn = new NpgsqlConnection(newconnectionString)){
                    conn.Open();
                    string amount_Query = @"
                        SELECT SUM(o.amount) FROM offering_box o
                        JOIN users u ON o.user_id = u.user_id
                        WHERE u.user_name = @user_name";

                    using (var cmd = new NpgsqlCommand(amount_Query, conn))
                    {
                        cmd.Parameters.AddWithValue("@user_name", user_name);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value)
                        {
                            total_amount = Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
            return total_amount;
        }
        
        // 残高計算コマンド
        public int balance_calculation_command(string user_name)
        {
            int total_offering = offering_box_price_command(user_name);
            int total_purchase = purchase_price_command(user_name);
            int balance = total_offering - total_purchase;
            return balance;
        }       
        
    }
}