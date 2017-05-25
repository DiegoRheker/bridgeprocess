using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace SchutzAPI
{
    class Util
    {
        public static string programInfo = " - ERROR:"
            + Environment.NewLine + Environment.NewLine + "Provide executable and both arguments >> SchutzAPI.exe [Mode][Store]"
            + Environment.NewLine + Environment.NewLine + "Argument [Mode]  > [PRODUCTS] - retrieve product list"
            + Environment.NewLine  + "Argument [Mode]  > [PULL] - Pull Stitch product list - not yet available"            
            + Environment.NewLine + "Argument [Mode]  > [REFORMAT] - append all SKU with Size, resort Size and Color columns"
            + Environment.NewLine + "Argument [Mode]  > [UPDATE] - Update store with Stitch product details - not yet available"  
            + Environment.NewLine + "Argument [Store] > [ECOM],[NY],[LA],[STITCH] - set API endpoint";

        private static string stitch_table_file = ConfigurationManager.AppSettings.Get("STITCH_table");

        public static void CheckNodeNames(XmlNodeList nodes)
        {
            foreach (XmlNode node in nodes)
            {
                // Do something with the node.
                if (node.Name.Contains("num"))
                {
                    Console.WriteLine("CheckNodeNames - " + node.Name + "<BR>");
                }

                if (node.HasChildNodes)
                {
                    CheckNodeNames(node.ChildNodes);
                }
            }
        }

        public static string ProperCase(string text)
        {
            TextInfo myTI = new CultureInfo("en-US", false).TextInfo;
            return myTI.ToTitleCase(text.ToLower());
        }    

        public static int GetShopifyCount()
        {
            //https://ecommerce.shopify.com/c/shopify-apis-and-technology/t/paginate-api-results-113066
            //<count type="integer">1270</count>
            Int32 productsCount = 0;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Program.xmlData);
            XmlNodeList cnt = doc.GetElementsByTagName("count");
            productsCount = Convert.ToInt32(cnt[0].InnerText.ToString());
            return productsCount;
        }

        public static string LineNum(Int32 i)
        {
            string s = i.ToString();
            if (i < 1000) { s = "0" + s; }
            if (i < 100) { s = "0" + s; }
            if (i < 10) { s = "0" + s; }
            return "line>" + s + "< ";
        }

        public static string LineNum2(Int32 i)
        {
            string s = i.ToString();
            if (i < 10) { s = "0" + s; }
            return " pos>" + s + "<";
        }

        public static string LineNum3(Int32 i)
        {
            string s = i.ToString();
            if (i < 100) { s = "0" + s; }
            if (i < 10) { s = "0" + s; }
            return s;
        }

        public static void Log(string message)
        {
            string line = DateTime.Now.ToString() + " " + message;
            WriteLog(line);
            Console.Write(line + Environment.NewLine);
        }

        public static string MakeJSONField(string val, string name)
        {
            if (string.IsNullOrEmpty(val) || val == "(NULL)")
            {
                val = "null";
            }
            else
            {
                //check string data types
                if (name == "title"
                    || name == "name"
                    || name == "option1"
                    || name == "option2"
                    || name == "option3"
                    || name == "sku"
                    || name == "action") { val = "\"" + val + "\""; }
            }
            string s = "\"" + name + "\":" + val.Trim();

            if (name != "option3"
                && name != "position"
                && name != "page_num") { s += ","; }

            return s;
        }

        public static string ParseSize(Double i)
        {
            try
            {
                string s = i.ToString().Replace(".", "");
                if (s.Length < 2) { s = s + "0"; }
                if (i < 10) { s = "0" + s; }
                //check 10 and 11
                if (s.Length < 3) { s = s + "0"; }
                return s;
            }
            catch (Exception ex)
            {
                Log("ParseSize(): " + i.ToString() + " " + ex.Message);
                return "_err";
            }
        }

        public static string ParseXmlNode(XmlNode n)
        {
            string val = string.Empty;
            try
            {
                if (n != null)
                {
                    val = n.InnerText.ToString().Trim();
                }
                if(string.IsNullOrEmpty(val)) 
                { 
                    val = "(NULL)"; 
                }
                return val;
            }
            catch (Exception ex)
            {
                Util.Log("ParseXmlNode() - Error: " + ex.Message);
                return "_err";
            }
        }

        public static void WriteProductsXML(String message)
        {
            try
            {
                string logfile = Program.product_log_file;
                logfile += "products_" + Program.STORE + " _" + Program.TICKS + ".xml";
                Program.get_file_name = logfile;
                using (StreamWriter swLog = File.AppendText(logfile))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Log("WriteProductsXML() - Error: " + ex.Message);
            }
        }

        public static void WriteProductsSingleXML(string message, string productID)
        {
            try
            {
                //D:\Websites\SchutzAPI\products_singles\{0}_PRODUCT_{1}_{2}.xml
                string temp = Program.product_log_file;
                string logfile = string.Format(temp,Program.STORE, productID, Program.TICKS);
                Program.get_file_name = logfile;
                using (StreamWriter swLog = File.AppendText(logfile))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Log("WriteProductsSingleXML() - Error: " + ex.Message);
            }
        }

        public static void WriteLog(String message)
        {
            try
            {
                using (StreamWriter swLog = File.AppendText(Program.run_file_name))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("WriteLog() - Error: " + ex.Message);
            }
        }

        public static void WriteSKULog(string message, string SKU)
        {
            try
            {
                string log_file = Program.run_file_name;
                bool found = false;
                if (SKU.Contains("U"))
                {
                    log_file = log_file.Replace("RUNLOG", "SKU_exception_inventory_" + Program.STORE + "_");
                    found = true;
                }
                if (SKU.Contains("X"))
                {
                    log_file = log_file.Replace("RUNLOG", "SKU_exception_multiples_" + Program.STORE + "_");
                    found = true;
                }
                if (found)
                {
                    using (StreamWriter swLog = File.AppendText(log_file))
                    {
                        swLog.WriteLine(message);
                        swLog.Flush();
                        swLog.Close();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("WriteSKULog() - Error: " + ex.Message);
            }
        }

        public static void WriteStitchTable(String message)
        {
            try
            {
                using (StreamWriter swLog = File.AppendText(stitch_table_file))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Log("WriteStitchTable() - Error: " + ex.Message);
            }
        }

        public static void WriteVariantsJSON(string message, Int32 page)
        {
            try
            {
                string logFile = Program.product_log_file;
                logFile += "variants_" + Program.STORE + "_page" + LineNum3(page) + " _" + Program.TICKS + ".json";
                Program.get_file_name = logFile;
                using (StreamWriter swLog = File.AppendText(logFile))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Util.Log("WriteVariantsJSON() - Error: " + ex.Message);
            }
        }

        public static void WriteVariantsXML(string message, Int32 page)
        {
            try
            {
                string logFile = Program.product_log_file;
                logFile += "variants_" + Program.STORE + "_page" + LineNum3(page) + " _" + Program.TICKS + ".xml";
                Program.get_file_name = logFile;
                using (StreamWriter swLog = File.AppendText(logFile))
                {
                    swLog.WriteLine(message);
                    swLog.Flush();
                    swLog.Close();
                }

            }
            catch (Exception ex)
            {
                Util.Log("WriteVariantsXML() - Error: " + ex.Message);
            }
        }

    }
}
