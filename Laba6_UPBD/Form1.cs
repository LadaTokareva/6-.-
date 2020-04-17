using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data;
using System.Timers;

namespace Laba6_UPBD
{
    public partial class Dgv_Shops : Form
    {
        private readonly string sConnStr = new SqlConnectionStringBuilder
        {
            DataSource = "LADA",
            InitialCatalog = "Shops",
            IntegratedSecurity = true
        }.ConnectionString;

        public Dgv_Shops()
        {
            InitializeComponent();
            InitializeDgvShop();
            InitializeDgvCity();
        }

        private void InitializeDgvShop()
        {
            dgvShops.Rows.Clear();
            dgvShops.Columns.Clear();
            var CityNameColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = @"Название города",
                Name = "name",
                DisplayMember = "name",
                ValueMember = "id_city"
            };
            var IdShop = new DataGridViewTextBoxColumn
            {
                Name = "id_shop",
                Visible = false
            };
            var AddressShop = new DataGridViewTextBoxColumn
            {
                HeaderText = "Адрес магазина",
                Name = "address"
            };
            var IsDelivery = new DataGridViewCheckBoxColumn
            {
                Name = "IsDelivery",
                HeaderText = @"Наличие сведений о поставках",
                ReadOnly = true
            };
            dgvShops.Columns.AddRange(IdShop, CityNameColumn, AddressShop, IsDelivery);
            
            using (var sConn = new SqlConnection(sConnStr))
            {
                sConn.Open();
                using (var sCommand = new SqlCommand())
                {
                    sCommand.CommandText = @"SELECT * FROM City";
                    sCommand.Connection = sConn;
                    var table = new DataTable();
                    table.Load(sCommand.ExecuteReader());
                    CityNameColumn.DataSource = table;
                }
                using (var sCommand = new SqlCommand())
                {
                    sCommand.Connection = sConn;
                    sCommand.CommandText = @"SELECT *, (CASE WHEN EXISTS (SELECT * FROM Delivery WHERE id_shop = Shop.id_shop) 
                                                        THEN 1 ELSE 0 END) AS IsDelivery
                                            FROM Shop join City on Shop.id_city=City.id_city";
                    var reader = sCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        var rowIdx = dgvShops.Rows.Add(reader["id_shop"], reader["Id_city"], reader["address"],
                            reader["IsDelivery"]);
                    }
                };
            }
        } 

        private void InitializeDgvCity()
        {
            dgvCity.Rows.Clear();
            dgvCity.Columns.Clear();
            dgvCity.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "id_city",
                Visible = false
            });
            dgvCity.Columns.Add("name", "Название города");
            dgvCity.Columns.Add("population", "Числен. населения");
            dgvCity.Columns.Add("average_salary_level", "Средняя зараб. плата");
            dgvCity.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "IsCity",
                HeaderText = @"Наличие магазина в этом городе",
                ReadOnly = true
            });

            using (var sConn = new SqlConnection(sConnStr))
            {
                sConn.Open();
                var sCommand = new SqlCommand
                {
                    Connection = sConn,
                    CommandText = @"SELECT id_city, name, population,average_salary_level, (CASE 
                                        WHEN EXISTS (SELECT * FROM Shop WHERE id_city = City.id_city) 
                                        THEN 1 ELSE 0 END) AS IsCity
                                    FROM City"
                };
                var reader = sCommand.ExecuteReader();
                while (reader.Read())
                {
                    //создаем словарик
                    var dataDict = new Dictionary<string, object>();
                    foreach (var columnsName in new[] { "name", "population", "average_salary_level" })
                    {
                        dataDict[columnsName] = reader[columnsName];
                    }
                    var rowIdx = dgvCity.Rows.Add(reader["id_city"], reader["name"], reader["population"],
                        reader["average_salary_level"], reader["IsCity"]);
                    //возвращаем id нашей вставленной строки
                    dgvCity.Rows[rowIdx].Tag = dataDict;
                }
            }
        }

        private void dgvProviders_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            var row = dgvShops.Rows[e.RowIndex];
            //изменялась ли строка
            if (dgvShops.IsCurrentRowDirty)
            {
                //создаем массив из эл-ов которые могут быть изменены 
                var cellsWithPotentialErrors = new[] { row.Cells["address"], row.Cells["name"]};
                //делаем проверку по пустым данным
                foreach (var cell in cellsWithPotentialErrors)
                {
                    //узнаем не пустое ли знач осталось
                    //if (string.IsNullOrWhiteSpace((string)cell.Value))
                    if (cell.Value is null) 
                    {
                        row.ErrorText = string.Format("Значение в столбце '{0}' не может быть пустым",
                            cell.OwningColumn.HeaderText);
                        e.Cancel = true;
                    }
                }
                if (!e.Cancel)
                {
                    using (var sConn = new SqlConnection(sConnStr))
                    {
                        sConn.Open();
                        var sCommand = new SqlCommand
                        {
                            Connection = sConn
                        };
                        sCommand.Parameters.AddWithValue("@MuName", row.Cells["name"].Value);
                        sCommand.Parameters.AddWithValue("@MuAddress", row.Cells["address"].Value);
                        var muId = (int?)row.Cells["id_shop"].Value;
                        if (muId.HasValue)
                        {
                            sCommand.CommandText = @"update Shop set id_city = @MuName, 
                                                        [address]=@MuAddress where id_shop=@MuId_Shop";
                            sCommand.Parameters.AddWithValue("@MuId_Shop", muId.Value);
                            sCommand.ExecuteNonQuery();
                        }
                        else
                        {
                            sCommand.CommandText = @"INSERT INTO Shop(address, id_city)
                                                        OUTPUT inserted.id_shop
                                                        VALUES (@MuAddress, @MuName)";
                            row.Cells["id_shop"].Value = sCommand.ExecuteScalar();
                        }
                        var dataDict = new Dictionary<string, object>();
                        foreach (var columnsName in new[] { "name", "address"})
                        {
                            dataDict[columnsName] = row.Cells[columnsName].Value;
                        }
                        row.Tag = dataDict;
                    }
                    row.ErrorText = "";
                    foreach (var cell in cellsWithPotentialErrors)
                    {
                        cell.ErrorText = "";
                    }
                    InitializeDgvCity();
                }
            }
        }

        private void dgvCity_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            var row = dgvCity.Rows[e.RowIndex];
            //изменялась ли строка
            if (dgvCity.IsCurrentRowDirty)
            {
                //создаем массив из эл-ов которые могут быть изменены 
                var cellsWithPotentialErrors = new[] { row.Cells["name"], row.Cells["population"], row.Cells["average_salary_level"] };
                //делаем проверку по пустым данным
                foreach (var cell in cellsWithPotentialErrors)
                {
                    //узнаем не пустое ли знач осталось
                    //if (!(cell.Value is int) && string.IsNullOrWhiteSpace((string)cell.Value))
                    if (cell.Value is null)
                    {
                        row.ErrorText = string.Format("Значение в столбце '{0}' не может быть пустым",
                            cell.OwningColumn.HeaderText);
                        e.Cancel = true;
                    }
                    int f = 0;
                    if (e.Cancel == false && !(int.TryParse((string)(row.Cells["population"].Value.ToString()), out f)))
                    {
                        row.ErrorText = string.Format("Значение в столбце '{0}' должно иметь целый тип", "Числен.населения");
                        e.Cancel = true;
                    }
                    if (e.Cancel == false && !(int.TryParse((string)(row.Cells["average_salary_level"].Value.ToString()), out f)))
                    {
                        row.ErrorText = string.Format("Значение в столбце '{0}' должно иметь целый тип", "Средняя зараб.плата");
                        e.Cancel = true;
                    }
                    if (e.Cancel == false && !(int.TryParse((string)(cell.Value.ToString()), out f)))
                        //((Dictionary<string, object>)dgvCity.Rows[e.RowIndex].Tag)[dgvCity.Columns[e.ColumnIndex].Name] is string)
                        using (var sConn = new SqlConnection(sConnStr))
                        {
                            sConn.Open();
                            var sCommand = new SqlCommand
                            {
                                Connection = sConn,
                                CommandText = @"select count(name) from City where name = '" + row.Cells["name"].Value + "'"
                            };
                            var muId = (int?)row.Cells["id_city"].Value;
                            if (muId.HasValue)
                            {
                                if ((int)sCommand.ExecuteScalar() == 1 && cell.Value != ((Dictionary<string, object>)dgvCity.Rows[e.RowIndex].Tag)[dgvCity.Columns[1].Name])
                                {
                                    row.ErrorText = string.Format("Значение в столбце '{0}' не должно повторяться", "Название города");
                                    e.Cancel = true;
                                }
                            }
                            else if ((int)sCommand.ExecuteScalar() == 1)
                            {
                                row.ErrorText = string.Format("Значение в столбце '{0}' не должно повторяться", "Название города");
                                e.Cancel = true;
                            }
                        }
                }
     
                if (!e.Cancel)
                {
                    using (var sConn = new SqlConnection(sConnStr))
                    {
                        sConn.Open();
                        var sCommand = new SqlCommand
                        {
                            Connection = sConn
                        };
                        sCommand.Parameters.AddWithValue("@MuName", row.Cells["name"].Value);
                        sCommand.Parameters.AddWithValue("@MuPopulation", row.Cells["population"].Value);
                        sCommand.Parameters.AddWithValue("@MuAvgSal", row.Cells["average_salary_level"].Value);
                        var muId = (int?)row.Cells["id_city"].Value;
                        if (muId.HasValue)
                        {
                            sCommand.CommandText = @"UPDATE City SET name = @MuName, population = @MuPopulation, average_salary_level = @MuAvgSal
                                                    WHERE id_city = @MuId_city";
                            sCommand.Parameters.AddWithValue("@MuId_city", muId.Value);
                            sCommand.ExecuteNonQuery();
                        }
                        else
                        {
                            sCommand.CommandText = @"INSERT INTO City(name, population,average_salary_level)
                                                        OUTPUT inserted.id_city
                                                        VALUES (@MuName, @MuPopulation, @MuAvgSal)";
                            row.Cells["id_city"].Value = sCommand.ExecuteScalar();
                        }
                        var dataDict = new Dictionary<string, object>();
                        foreach (var columnsName in new[] { "name", "population", "average_salary_level" })
                        {
                            dataDict[columnsName] = row.Cells[columnsName].Value;
                        }
                        row.Tag = dataDict;
                        InitializeDgvShop();
                    }
                    row.ErrorText = "";
                    foreach (var cell in cellsWithPotentialErrors)
                    {
                        cell.ErrorText = "";
                    }
                    //row.Cells["population"].ErrorText = "";
                    //row.Cells["average_salary_level"].ErrorText = "";
                }
            }
        }

        private void dgvProviders_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!dgvShops.Rows[e.RowIndex].IsNewRow)
            {
                dgvShops[e.ColumnIndex, e.RowIndex].ErrorText = "Значение изменено и пока не сохранено.";
                /*if (dgvShops.Rows[e.RowIndex].Tag != null)
                    dgvShops[e.ColumnIndex, e.RowIndex].ErrorText += "\nПредыдущее значение - " +
                    ((Dictionary<string, object>)dgvShops.Rows[e.RowIndex].Tag)[dgvShops.Columns[e.ColumnIndex].Name];*/
            }
        }

        private void dgvProviders_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            var muId = (int?)e.Row.Cells["id_shop"].Value;
            if (muId.HasValue)
            {
                using (var sConn = new SqlConnection(sConnStr))
                {
                    sConn.Open();
                    var sCommand = new SqlCommand
                    {
                        Connection = sConn,
                        CommandText = "DELETE FROM Shop WHERE id_shop = @MuID_Shop"
                    };
                    sCommand.Parameters.AddWithValue("@MuID_Shop", muId.Value);
                    sCommand.ExecuteNonQuery();
                }
                InitializeDgvCity();
            }
        }


        private void dgvCity_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            var muId = (int?)e.Row.Cells["id_city"].Value;
            if (muId.HasValue)
            {
                
                using (var sConn = new SqlConnection(sConnStr))
                {
                    sConn.Open();
                    var sCommand = new SqlCommand
                    {
                        Connection = sConn,
                        CommandText = "DELETE FROM City WHERE id_city = @MuID_City"
                    };
                    sCommand.Parameters.AddWithValue("@MuID_City", muId.Value);
                    var sCommand1 = new SqlCommand()
                    {
                        Connection = sConn,
                        CommandText = @"select count(address) from Shop where id_city = @MuID_City"
                    };
                    sCommand1.Parameters.AddWithValue("@MuID_City", muId.Value);
                    if ((int)sCommand1.ExecuteScalar() == 0)
                    {
                        sCommand.ExecuteNonQuery();
                    }
                    else
                    {
                        e.Row.ErrorText = "Нельзя удалить город, если в нем есть магазин!!!";
                        e.Cancel = true;
                    }
                }
               
            }
        }

        private void dgvCity_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!dgvCity.Rows[e.RowIndex].IsNewRow)
            {
                dgvCity[e.ColumnIndex, e.RowIndex].ErrorText = "Значение изменено и пока не сохранено.";
                if (dgvCity.Rows[e.RowIndex].Tag != null)
                    dgvCity[e.ColumnIndex, e.RowIndex].ErrorText += "\nПредыдущее значение - " +
                    ((Dictionary<string, object>)dgvCity.Rows[e.RowIndex].Tag)[dgvCity.Columns[e.ColumnIndex].Name];
            }
        }

        private void dgvCity_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && dgvCity.IsCurrentRowDirty)
            {
                dgvCity.CancelEdit();
                if (dgvCity.CurrentRow.Cells["id_city"].Value != null)
                {
                    foreach (var kvp in (Dictionary<string, object>)dgvCity.CurrentRow.Tag)
                    {
                        dgvCity.CurrentRow.Cells[kvp.Key].Value = kvp.Value;
                        dgvCity.CurrentRow.Cells[kvp.Key].ErrorText = "";
                    }
                }
                else
                {
                    dgvCity.Rows.Remove(dgvShops.CurrentRow);
                }
            }
        }

        private void Dgv_Shops_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void dgvShops_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
