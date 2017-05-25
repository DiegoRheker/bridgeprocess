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
    class Shopify
    {
        public static void GetProductList()
        {
            Int32 productsCount = 0;
            Program._params = string.Empty;
            Program.post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsCountLocation");
            Program.xmlData = Program.DataGet(Program._params);

            if (Program.xmlData != "ERROR")
            {
                productsCount = Util.GetShopifyCount();
            }

            if (productsCount != 0)
            {
                Util.Log("Product count: " + productsCount.ToString());
                Double page_size = 250;
                Double pages = Math.Ceiling(productsCount / page_size);
                Program.xmlData = string.Empty;

                Program.post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsGetLocation");
                Util.Log("- GET_location: " + Program.post_location);
                //<= pages
                for (int i = 1; i <= pages; ++i)
                {
                    Util.Log("- Page: " + i.ToString());
                    Util.Log("X_Shopify_Shop_Api_Call_Limit: " + Program.X_Shopify_Shop_Api_Call_Limit);
                    Program._params = "?limit=250&page=" + i.ToString();
                    Program.xmlData += Program.DataGet(Program._params);
                }
                //cleanup multiple root elements in data, as this is a composite XML doc
                //<?xml version="1.0" encoding="UTF-8"?>
                //<products type="array">
                //</products>
                Program.xmlData = Program.xmlData.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "");
                Program.xmlData = Program.xmlData.Replace("<products type=\"array\">", "");
                Program.xmlData = Program.xmlData.Replace("</products>", "");
                //remove 2nd level parent nodes
                Program.xmlData = Program.xmlData.Replace("<variants type=\"array\">", "");
                Program.xmlData = Program.xmlData.Replace("</variants>", "");
                Program.xmlData = Program.xmlData.Replace("<options type=\"array\">", "");
                Program.xmlData = Program.xmlData.Replace("</options>", "");

                //add proper format
                Program.xmlData =
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<products type=\"array\">"
                    + Program.xmlData
                    + "</products>";

                Util.WriteProductsXML(Program.xmlData);
            }
        }

        public static void GetProductIDList()
        {
            Int32 productsCount = 0;
            Program._params = string.Empty;
            Program.post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsCountLocation");
            Program.xmlData = Program.DataGet(Program._params);

            if (Program.xmlData != "ERROR")
            {
                productsCount = Util.GetShopifyCount();
            }

            Program.xmlData = string.Empty; //reset

            if (productsCount != 0)
            {
                Util.Log("Product count: " + productsCount.ToString());
                Double page_size = 250;
                Double pages = Math.Ceiling(productsCount / page_size);
                Program.xmlProducts = string.Empty;

                Program.post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsGetLocation");
                Util.Log("- GET_location: " + Program.post_location);

                for (int i = 1; i <= pages; ++i)
                {
                    Util.Log("- Page: " + i.ToString());
                    Util.Log("X_Shopify_Shop_Api_Call_Limit: " + Program.X_Shopify_Shop_Api_Call_Limit);
                    Program._params = "?fields=id&limit=250&page=" + i.ToString();
                    Program.xmlProducts += Program.DataGet(Program._params);
                }
                //cleanup multiple root elements in data, as this is a composite XML doc
                //<?xml version="1.0" encoding="UTF-8"?>
                //<products type="array">
                //</products>
                Program.xmlProducts = Program.xmlProducts.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "");
                Program.xmlProducts = Program.xmlProducts.Replace("<products type=\"array\">", "");
                Program.xmlProducts = Program.xmlProducts.Replace("</products>", "");
                //remove 2nd level parent nodes
                Program.xmlProducts = Program.xmlProducts.Replace("<product>", "");
                Program.xmlProducts = Program.xmlProducts.Replace("</product>", "");
                Program.xmlProducts = Program.xmlProducts.Replace(" type=\"integer\"", "");
                //add proper format
                Program.xmlProducts =
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<products>"
                    + Program.xmlProducts
                    + "</products>";

                Util.WriteProductsXML(Program.xmlProducts);
            }
        }

        public static void ReformatProducts()
        {
            //https://help.shopify.com/api/reference/product
            //https://help.shopify.com/api/reference/product_variant
            //https://schutzonline.myshopify.com/admin/variants/29678925251.json
            //adding options:
            //https://ecommerce.shopify.com/c/shopify-apis-and-technology/t/3-option-values-given-but-2-options-exist-307166
            Int32 i = 1;
            Int32 err = 0;
            try
            {
                Console.WriteLine("File to parse: " + Program.get_file_name); //keep copy as file
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(Program.xmlData); //load actual data from memory
                XmlNodeList products = doc.GetElementsByTagName("product");
                XmlNodeList variants;
                XmlNodeList options;
                string temp;
                Console.WriteLine("products count: " + products.Count.ToString());
                string SKU;
                string newSKU;
                double mySize;
                Int32 sizeCol = 0;
                Int32 vIndex = 1;
                string postJSON = string.Empty;
                string ProductID = string.Empty;
                string Title = string.Empty;
                bool hasError = false;
                string line = string.Empty;
                Program.post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsPutLocation");

                foreach (XmlNode product in products)
                {
                    hasError = false;
                    vIndex = 1;
                    postJSON = "{\"product\":{";
                    ProductID = product.SelectSingleNode("id").InnerText.Trim();
                    Program.product_put_location = string.Format(Program.post_location, ProductID).Replace(".json", "");
                    postJSON += Util.MakeJSONField(ProductID, "id");
                    Title = product.SelectSingleNode("title").InnerText.Trim();
                    if (Title.ToLower() == "test") { continue; }
                    postJSON += Util.MakeJSONField(Title, "title");
                    postJSON += "\"variants\":[";

                    variants = product.SelectNodes("variant");
                    options = product.SelectNodes("option");
                    Util.Log(Util.LineNum(i) + Program.product_put_location + " - variants count: " + variants.Count.ToString());
                    foreach (XmlNode variant in variants)
                    {
                        SKU = variant.SelectSingleNode("sku").InnerText.ToString().ToUpper(); //make sure to upp lower 's' and 'u'
                        newSKU = string.Empty;
                        mySize = 0;

                        //check which of 3 options has the size value
                        //check for empty node
                        sizeCol = 1;
                        if (!String.IsNullOrEmpty(variant.SelectSingleNode("option1").InnerText))
                        {
                            Double.TryParse(variant.SelectSingleNode("option1").InnerText.ToString(), out mySize);
                        }
                        if (mySize == 0)//scan until set
                        {
                            sizeCol = 2;
                            if (!String.IsNullOrEmpty(variant.SelectSingleNode("option2").InnerText))
                            {
                                Double.TryParse(variant.SelectSingleNode("option2").InnerText.ToString(), out mySize);
                            }
                        }
                        if (mySize == 0)//scan until set
                        {
                            sizeCol = 3;
                            if (!String.IsNullOrEmpty(variant.SelectSingleNode("option3").InnerText))
                            {
                                Double.TryParse(variant.SelectSingleNode("option3").InnerText.ToString(), out mySize);
                            }
                        }

                        //<sku>S0,157,900,430,010</sku>
                        //amend Size to SKU
                        if (!String.IsNullOrEmpty(SKU))
                        {
                            newSKU = string.Empty;
                            if (SKU.Contains("/"))
                            {
                                string[] s = SKU.Split('/');
                                SKU = s[s.Length - 1].Trim(); //take last in array
                                if (String.IsNullOrEmpty(SKU))
                                {
                                    SKU = s[s.Length - 2].Trim(); //take second last in array; some SKU have a trailing foreslash
                                }
                                SKU += "X"; //stamp for review                               
                            }
                            if (SKU.Length < 16 && mySize != 0)
                            {
                                newSKU = SKU + Util.ParseSize(mySize);
                                Util.Log(Util.LineNum(i) + Program.product_put_location + Util.LineNum2(vIndex) + " SKU: " + SKU + " - new SKU: " + newSKU);
                            }
                            else
                            {
                                Util.Log(Util.LineNum(i) + Program.product_put_location + Util.LineNum2(vIndex) + " SKU: " + SKU + " - skipped reformat / too long or already set, or size is null");
                            }
                            if (String.IsNullOrEmpty(newSKU)) //then not set
                            {
                                newSKU = SKU;
                            }
                            if (newSKU.Contains("U") || newSKU.Contains("X"))
                            {
                                Util.WriteSKULog(Util.LineNum(i) + Program.product_put_location + Util.LineNum2(vIndex) + " SKU: " + SKU + " - new SKU: " + newSKU, newSKU);
                            }
                        }

                        if (mySize == 0)
                        {
                            line = Util.LineNum(i) + " Size: " + Util.ParseSize(mySize) + " - error / invalid or null";
                            hasError = true;
                        }

                        if (!hasError) // can't resort size value if column unknown
                        {
                            //build each variant node
                            //{
                            //"id":29678925251,
                            //"product_id":8762362755,
                            //"sku":"S0157900430010U060",
                            //"option2":"SNAKE",
                            //"option1":"6",
                            //"option3":null
                            //},

                            postJSON += "{";
                            postJSON += Util.MakeJSONField(variant.SelectSingleNode("id").InnerText, "id");
                            postJSON += Util.MakeJSONField(variant.SelectSingleNode("product-id").InnerText, "product-id");
                            postJSON += Util.MakeJSONField(newSKU, "sku");
                            string size_option = string.Empty;
                            string color_option = string.Empty;
                            string material_option = string.Empty;
                            string temp_option1 = string.Empty;
                            string temp_option2 = string.Empty;
                            string temp_option3 = string.Empty;
                            if (!String.IsNullOrEmpty(variant.SelectSingleNode("option1").InnerText))
                            {
                                temp_option1 = variant.SelectSingleNode("option1").InnerText;
                            }
                            if (!String.IsNullOrEmpty(variant.SelectSingleNode("option2").InnerText))
                            {
                                temp_option2 = variant.SelectSingleNode("option2").InnerText;
                            }
                            if (!String.IsNullOrEmpty(variant.SelectSingleNode("option3").InnerText))
                            {
                                temp_option3 = variant.SelectSingleNode("option3").InnerText;
                            }
                            //resort col position of option values
                            //for eCom option 3 material can be empty
                            //when size is not option 1, then color is always option 1
                            //check sizeCol value and move columns
                            if (options.Count == 1)
                            {
                                //can't move option value if only size col exists
                                //sorting is OK
                                size_option = temp_option1;
                                color_option = temp_option2;
                                material_option = temp_option3;
                                //leave size in first place, other two options are null
                                postJSON += Util.MakeJSONField(size_option, "option1");
                                postJSON += Util.MakeJSONField(color_option, "option2");
                                postJSON += Util.MakeJSONField(material_option, "option3");
                            }
                            else
                            {
                                if (sizeCol == 1)
                                {
                                    //sorting is OK
                                    size_option = temp_option1;
                                    color_option = temp_option2;
                                    material_option = temp_option3;
                                }
                                else if (sizeCol == 2)
                                {
                                    //switch color and size; material is never in 1st colum
                                    size_option = temp_option2;
                                    color_option = temp_option1;
                                    material_option = temp_option3;
                                }
                                else if (sizeCol == 3)
                                {
                                    //switch color, material and size; size is always 1st colum
                                    size_option = temp_option3;
                                    color_option = temp_option1;
                                    material_option = temp_option2;
                                }
                                //sorting of values is *now* size/color/material
                                //finally set the field name it is saved into
                                //put color first, size second - makes more sense for shoes with multiple colors
                                postJSON += Util.MakeJSONField(size_option, "option2");
                                postJSON += Util.MakeJSONField(color_option, "option1");
                                postJSON += Util.MakeJSONField(material_option, "option3");
                            }
                            postJSON += "},";
                        }
                        vIndex++;
                    }//foreach variant

                    if (!hasError)
                    {
                        temp = postJSON.TrimEnd(',');
                        postJSON = temp;
                        postJSON += "]"; //close variants
                        postJSON += ",";
                        postJSON += "\"options\":[";

                        //build each option node
                        //{
                        //"id":10520462787,
                        //"product_id":8762362755,
                        //"name":"Color",
                        //"position":2,
                        //"values":["SNAKE"]
                        //}

                        Util.Log(Util.LineNum(i) + Program.product_put_location + " - options count: " + options.Count.ToString());
                        string options1 = string.Empty;
                        string options2 = string.Empty;
                        string options3 = string.Empty;
                        string options_temp = string.Empty;
                        string options_type = string.Empty;
                        bool colorFound = false;
                        foreach (XmlNode option in options)
                        {
                            options_temp = "{";
                            options_temp += Util.MakeJSONField(option.SelectSingleNode("id").InnerText, "id");
                            options_temp += Util.MakeJSONField(option.SelectSingleNode("product-id").InnerText, "product-id");
                            options_type = option.SelectSingleNode("name").InnerText;
                            if (options_type.ToLower() == "title")//then switch
                            {
                                options_type = "Color";
                            }
                            options_temp += Util.MakeJSONField(options_type, "name");
                            options_temp += Util.MakeJSONField(option.SelectSingleNode("position").InnerText, "position");
                            options_temp += "},";
                            //sort options
                            if (options_type.ToLower() == "size")
                            {
                                options2 = options_temp;
                            }
                            else if (options_type.ToLower() == "color")
                            {
                                options1 = options_temp;
                                colorFound = true;
                            }
                            else if (options_type.ToLower() == "material")
                            {
                                options3 = options_temp;
                            }
                        }
                        if (!colorFound) //can't move size to option2
                        {
                            postJSON += options2 + options3;
                        }
                        else
                        {
                            postJSON += options1 + options2 + options3;
                        }
                        temp = postJSON.TrimEnd(',');
                        postJSON = temp;
                        postJSON += "]";//close options
                        postJSON += "}";//close product
                        postJSON += "}";//close envelope

                        Program.product_put_location += ".json"; //add back in for data post
                        Util.Log(Util.LineNum(i) + Program.product_put_location + " PUT_DATA_PACKET: " + postJSON);

                        if (Program.AppMode.ToLower() == "prod")
                        {
                            string result = Program.DataPost(Program.product_put_location, postJSON);
                            if (result != "POSTSUCCESS")//abort & review logfile
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        Util.Log(line);
                        err++;
                    }
                    i++;
                }//foreach product
                Util.Log("Success: " + (i - 1 - err).ToString() + " - Error: " + err.ToString());
            }
            catch (Exception ex)
            {
                Util.Log("Error - ReformatProducts(): " + ex.Message + " " + ex.StackTrace.ToString());
            }
        }

        public static void CheckLeakyBucket(string X_Shopify_Shop_Api_Call_Limit)
        {
            string[] s = X_Shopify_Shop_Api_Call_Limit.Split('/');
            string line = string.Empty;
            Int32 calls = Convert.ToInt32(s[0]);
            Int32 limit = Convert.ToInt32(s[1]);
            if (calls + 1 == limit)
            {
                line = DateTime.Now.ToString() + " LeakyBucket break @ " + X_Shopify_Shop_Api_Call_Limit + Environment.NewLine;
                Util.Log(line);
                Int32 duration = Convert.ToInt32(ConfigurationManager.AppSettings.Get("LeakyBucketBreakMiliSec"));
                System.Threading.Thread.Sleep(duration);
                line = DateTime.Now.ToString() + " LeakyBucket continue after break" + Environment.NewLine;
            }
        }
    }
}
