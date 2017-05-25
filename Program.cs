using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace SchutzAPI
{
    class Program
    {        
        public static string AppMode = ConfigurationManager.AppSettings.Get("AppMode");
        public static string STORE;
        public static string TICKS;
        public static bool DEBUG = (ConfigurationManager.AppSettings.Get("DEBUG") == "true");
        public static string _params;
        public static string get_file_name;
        public static string run_file_name;
        private static string line = string.Empty;
        public static string xmlData;
        public static string xmlProducts;
        public static string post_location;
        public static string product_put_location;
        public static string product_log_file = ConfigurationManager.AppSettings.Get("ProductsFile");
        private static string post_response;
        public static string X_Shopify_Shop_Api_Call_Limit;
        private static IWebProxy DefaultWebProxy { get; set; }


        static void Main(string[] args)
        {
            
            string sMode = string.Empty;
            TICKS = DateTime.Now.Ticks.ToString();
            run_file_name = ConfigurationManager.AppSettings.Get("LogFile") + "RUNLOG_" + sMode + "_" + STORE + "_" + Program.TICKS + ".txt";

            if (args.Length != 0)
            {
                sMode = args[0].ToString().ToUpper(); //mode  
                if (args.Length > 1)
                {
                    STORE = args[1].ToString().ToUpper(); //get store
                }
                else //abort
                {
                    Util.Log(Util.programInfo);
                    return;
                }

                run_file_name = ConfigurationManager.AppSettings.Get("LogFile") + "RUNLOG_" + sMode + "_" + STORE + "_" + Program.TICKS + ".txt";
            
                Util.Log("- START RUN - MODE: " + sMode + " - STORE: " + STORE);

                //post_location = ConfigurationManager.AppSettings.Get(STORE + "_ProductsGetLocation");
                //Util.Log("- post_location: " + post_location);

                if (sMode == "PRODUCTS")
                {
                    Shopify.GetProductList();
                }

                if (sMode == "PULL")
                {
                    if (ConfigurationManager.AppSettings.Get("STITCH_test") == "true")
                    {
                        Util.Log("- STITCH_test");
                        if (ConfigurationManager.AppSettings.Get("STITCH_make_test_file") == "true")
                        {
                            Util.Log("- STITCH_test_shopify_products_file");
                            Stitch.MakeStitchTableFile_Test();
                        }
                        else
                        {
                            Stitch.MakeStitchTableFile(1);
                        }
                        Stitch.LoadStitchTable();
                        //Stitch.PlotStitchTable();
                        //Stitch.UpdateProductsViaStitch();
                        Stitch.CrossRefOptions();
                    }
                    else
                    {
                        Stitch.GetStitchVariants();
                    }
                }

                if (sMode == "REFORMAT")
                {
                    Shopify.GetProductList();
                    Shopify.ReformatProducts();
                }

                if (sMode == "UPDATE")
                {
                    Shopify.GetProductIDList();                  
                    Stitch.UpdateProductsViaStitch();
                    //Stitch.PlotStitchTableUnmatched();
                }

                Util.Log("- END OF RUN");
            }
            else
            {
                Util.Log(Util.programInfo);
            }
        }

        public static string DataGet(string _params)
        {
            try
            {
                // Create a request for the URL. 
                WebRequest request = WebRequest.Create(post_location + _params);
                string username = ConfigurationManager.AppSettings.Get(STORE+"_ApiKey");
                string password = ConfigurationManager.AppSettings.Get(STORE+"_ApiPassword");
                string encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                request.Headers.Add("Authorization", "Basic " + encoded);
                // Get the response.
                WebResponse response = request.GetResponse();
                // Display the status.
                Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                // Get the stream containing content returned by the server.
                Stream dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.
                string responseFromServer = reader.ReadToEnd();
                // Clean up the streams and the response.

                reader.Close();
                response.Close();

                X_Shopify_Shop_Api_Call_Limit = response.Headers["X-Shopify-Shop-Api-Call-Limit"].ToString();
                Util.Log("X_Shopify_Shop_Api_Call_Limit: " + X_Shopify_Shop_Api_Call_Limit);
                Shopify.CheckLeakyBucket(X_Shopify_Shop_Api_Call_Limit);

                return responseFromServer;
            }
            catch (Exception ex)
            {
                Util.Log(": ERROR in DataGet(): " + ex.Message);
                return ex.Message;
            }
        }

        public static string DataPost(string postLocation, string postString)
        {
            try
            {
                // create an HttpWebRequest object to communicate with Shopify web service API
                HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(postLocation);
                string username = ConfigurationManager.AppSettings.Get(STORE+"_ApiKey");
                string password = ConfigurationManager.AppSettings.Get(STORE+"_ApiPassword");
                string encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                objRequest.Headers.Add("Authorization", "Basic " + encoded);
                objRequest.Proxy = DefaultWebProxy;
                objRequest.Method = "PUT";
                objRequest.ContentLength = postString.Length;
                objRequest.ContentType = "application/json";
                // post data is sent as a stream
                StreamWriter myWriter = null;
                myWriter = new StreamWriter(objRequest.GetRequestStream());
                myWriter.Write(postString);
                myWriter.Close();

                // returned values are returned as a stream, then read into a String              
                HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                {
                    post_response = responseStream.ReadToEnd();
                    responseStream.Close();
                }
                objResponse.Close();

                X_Shopify_Shop_Api_Call_Limit = objResponse.Headers["X-Shopify-Shop-Api-Call-Limit"].ToString();
                Util.Log("X_Shopify_Shop_Api_Call_Limit: " + X_Shopify_Shop_Api_Call_Limit);
                Shopify.CheckLeakyBucket(X_Shopify_Shop_Api_Call_Limit);

                if(post_response.Contains("error"))
                {
                    Util.Log(post_response);
                    return "FAILURE";
                }
                return "POSTSUCCESS";
            }
            catch (Exception ex)
            {
                Util.Log(": ERROR in DataPost(): " + ex.Message);
                return ex.Message;
            }
        }

    }
}
