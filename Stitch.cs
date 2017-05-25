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
using SchutzAPI;
using Newtonsoft.Json;

namespace SchutzAPI
{
    class Stitch
    {
        private static string StitchHeader;
        private static Int32 StitchPages = 1;
        private static Int32 StitchCounter = 0;
        private static Int32 StitchTotal = 0;
        private static bool hasStitchFileTableHeader = false;
        private static string xmlData;
        private static string xmlClone;
        private static string xmlTemp;
        private static XmlNode rootDocument;
        private static string post_location = ConfigurationManager.AppSettings.Get("STITCH_VariantsGetLocation");
        private static string stitch_table_file = ConfigurationManager.AppSettings.Get("STITCH_table");
        private static string line;
        private static string post_response;
        private static IWebProxy DefaultWebProxy { get; set; }
        private static Int32 colIndex;
        private static Int32 rowIndex;
        private static Int32 checksum_SKU_per_product = 0;
        public static DataTable dtStitchTable;
        public static DataRow dr;


        private static void FixNumericalNodeNames(XmlNodeList nodes)
        {
            foreach (XmlNode node in nodes)
            {
                //check node is numeric and update name + value.
                decimal num = 0;
                bool isNumeric = Decimal.TryParse(node.Name, out num);
                if (isNumeric)
                {
                    if (Program.DEBUG) { Util.Log("FixNumericNodeNames - replace: " + node.Name + " // width: num" + node.Name); }
                    //note: since replace is global in string xmlClone, 
                    //node.name plus id.value will be prefixed with ‘num’
                    xmlTemp = xmlClone.Replace(node.Name, "num" + node.Name);
                    xmlClone = xmlTemp;
                }
                if (node.HasChildNodes)//recursive search
                {
                    FixNumericalNodeNames(node.ChildNodes);
                }
            }
        }

        public static void GetStitchVariants()
        {
            //Pagination is calculated by the server and sent back at the top of the response - here’s an example:
            //  "meta": {
            //    "current_page": "1",
            //    "last_page": "2",
            //    "per_page": "50",
            //    "total": "92",
            //    "from": "1",
            //    "to": "50"
            //  },
            //You’d pass along the parameters in the body of the POST (currently everything in the front-end API is done through POST)
            //{
            //  "action": "read",
            //  "page_num": 1,
            //  "page_size": 50
            //}

            if (File.Exists(stitch_table_file)) { File.Delete(stitch_table_file); }
            StitchCounter = 0; //reset
            xmlData = string.Empty;
            string response = string.Empty;
            Util.Log("START MAKE STITCH TABLE");
            for (int page = 1; page <= StitchPages; page++)
            {
                //POST https://api-pub.stitchlabs.com/api2/v2/Variants
                Util.Log("GetStitchVariants() - page: " + Util.LineNum3(page) + " of " + StitchPages.ToString());
                if (StitchPages == 1)
                {
                    Util.Log("(- first pull returns page count -)");
                }
                response = StitchDataGet(MakeStitchHeader(page)); //appends xmlData

                if (response == "POSTSUCCESS")
                {
                    if (StitchPages == 1)
                    {
                        //find page number in first JSON formatted response
                        int first = xmlData.IndexOf("\"last_page\":") + "\"last_page\":".Length + 1;
                        string temp = xmlData.Substring(first, 25);
                        int end = temp.IndexOf(",");
                        string pages = temp.Substring(0, end).Replace("\"", "");
                        int n;
                        bool isNumeric = int.TryParse(pages, out n);
                        if (isNumeric)//update count
                        {
                            StitchPages = Convert.ToInt32(pages);
                        }
                        first = xmlData.IndexOf("\"total\":") + "\"total\":".Length + 1;
                        temp = xmlData.Substring(first, 25);
                        end = temp.IndexOf(",");
                        string total = temp.Substring(0, end).Replace("\"", "");
                        n = 0;
                        isNumeric = int.TryParse(total, out n);
                        if (isNumeric)//update count
                        {
                            StitchTotal = Convert.ToInt32(total);
                        }
                    }
                    //append records
                    MakeStitchTableFile(page);
                    //xmlData is written in StitchDataGet()
                    //save entire JSON response before xml conversion
                    Util.WriteVariantsJSON(xmlData, page);
                    //reset
                    xmlData = string.Empty;
                }
                else
                {
                    Util.Log("GetStitchVariants() - ERROR: " + response);
                }
            }
            Util.Log("END MAKE STITCH TABLE");
            Util.Log("GetStitchVariants() - total counter:  " + StitchCounter.ToString() + " - Stitch checksum: " + StitchTotal.ToString());
        }

        private static bool IsValid(string data)
        {
            try
            {
                //test json is valid
                rootDocument = JsonConvert.DeserializeXmlNode(data, "root");
            }
            catch (Exception ex)
            {
                Util.Log("IsValid(JSON) - ERROR: " + ex.Message);
                return false;
            }
            try
            {
                //test xml is valid
                Int32 count = rootDocument.ChildNodes.Count;
            }
            catch (XmlException ex)
            {
                Util.Log("IsValid(XML) - ERROR: " + ex.Message);
                return false;
            }
            return true;
        }

        public static void LoadStitchTable()
        {
            dtStitchTable = new DataTable(); //make sure rows are not cached from prior run
            FileInfo tempFile = new FileInfo(stitch_table_file);
            StreamReader sr = new StreamReader(tempFile.FullName);
            bool headerRow = true;
            line = string.Empty;

            while ((line = sr.ReadLine()) != null)
            {
                dr = dtStitchTable.NewRow(); 
                colIndex = 0;
                string[] cols = line.Split(new Char[] { '\t' });
                foreach (string field in cols)
                {
                    if (headerRow)
                    {
                        dtStitchTable.Columns.Add(field);
                    }
                    else //data rows
                    {
                        dr[colIndex] = field;
                    }
                    colIndex++; 
                }
                //add tracking column
                if (headerRow)
                {
                    dtStitchTable.Columns.Add("MATCHED");
                }
                else
                {
                    colIndex++;
                    dr[colIndex] = 0; //set default not matched
                }

                headerRow = false;
                if (rowIndex > 0)
                {
                    dtStitchTable.Rows.Add(dr);
                }
                rowIndex++;
            }

        }

        private static string MakeStitchHeader(Int32 page)
        {
            StitchHeader = "{";
            StitchHeader += Util.MakeJSONField("read", "action");
            StitchHeader += Util.MakeJSONField("50", "page_size");
            StitchHeader += Util.MakeJSONField(page.ToString(), "page_num");
            StitchHeader += "}";
            return StitchHeader;
        }

        public static void MakeStitchTableFile(Int32 page)
        {
            Int32 i = 1;
            Int32 err = 0;
            try
            {
                if (ConfigurationManager.AppSettings.Get("STITCH_test") == "true")
                {
                    FileInfo tempFile = new FileInfo("D:\\Websites\\SchutzAPI\\concept_dev\\variants_sample2.txt");
                    StreamReader sr = new StreamReader(tempFile.FullName);
                    line = string.Empty;
                    xmlData = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                        xmlData += line;
                    }
                }

                xmlClone = xmlData.Replace("\\/","/"); //strip out JSON foreslash escape sequence's

                //now deserialize xmlData and test it's valid XML
                if (!IsValid(xmlData)) { return; }

                XmlNode root = rootDocument.FirstChild;

                Util.Log("START: FixNumericalNodeNames() ");
                //note: FixNumericalNodeNames reads xmlData but updates xmlClone
                FixNumericalNodeNames(root.ChildNodes);

                //now deserialize xmlClone
                if (!IsValid(xmlClone)) { return; }

                Util.Log("END: FixNumericalNodeNames() ");

                //save xml converted Stitch response as file
                Util.WriteVariantsXML(xmlClone,page);

                //we're good
                root = rootDocument.FirstChild;

                //CheckNodeNames(root.ChildNodes);

                XmlNodeList variantList = root.SelectNodes("Variants");
                Util.Log("Variants count: " + variantList.Count.ToString());
                string _auto_description = string.Empty;
                string _options = string.Empty;
                string _color = string.Empty;
                string _size = string.Empty;
                string _material = string.Empty;
                string _price = string.Empty;
                string _sku = string.Empty;
                string _upc = string.Empty;
                string _weight = string.Empty;
                XmlNode nPriceID;
                XmlNode nPrice;
                string _priceID = string.Empty;
                line = string.Empty; //reset
                //delete prior run file

                foreach (XmlNode variant in variantList)
                {
                    //<Variants>
                    //  <auto_description>Blue Shoe</auto_description>
                    //  <auto_description_with_option_type>Blue Shoe (Color: "Blue", Size: "5.5", Material: "Nobuck")</auto_description_with_option_type>
                    //  <id>230739675</id>
                    //  <links><VariantPrices><id>p243322791</id></VariantPrices>
                    //  </links>
                    //  <sku />
                    //  <supplier_cost>0.0000</supplier_cost>
                    //  <upc />
                    //  <updated_at>2016-11-23T13:59:01-05:00</updated_at>
                    //  <weight />
                    //</Variants>
                    //<VariantPrices>
                    //  <p243322791>
                    //    <created_at>2016-03-22T12:30:02-04:00</created_at>
                    //    <deleted>0</deleted>
                    //    <id>243322791</id>
                    //    <links>
                    //      <Variants><id>230739675</id></Variants>
                    //    </links>
                    //    <price>13.0000</price>
                    //    <updated_at>2016-11-23T13:59:01-05:00</updated_at>
                    //  </243322791>
                    //</VariantPrices>

                    StitchCounter++;
                    _auto_description = variant.SelectSingleNode("auto_description").InnerText;
                    _options = variant.SelectSingleNode("auto_description_with_option_type").InnerText;
                    _color = ParseOption(_options, 0);
                    _size = ParseOption(_options, 1);
                    _material = ParseOption(_options, 2);
                    _sku = variant.SelectSingleNode("sku").InnerText;
                    _upc = variant.SelectSingleNode("upc").InnerText;
                    _weight = variant.SelectSingleNode("weight").InnerText;
                    _price = "0.00";
                    try 
                    { 
                        nPriceID = variant.SelectSingleNode("./links/VariantPrices/id");
                        _priceID = nPriceID.InnerText;
                        string xPath = String.Format("VariantPrices/{0}/price", _priceID);
                        nPrice = root.SelectSingleNode(xPath);
                        if (nPrice != null)
                        {
                            decimal d = Convert.ToDecimal(nPrice.InnerText);
                            _price = Math.Round(d, 2).ToString(); //format precision 2
                        }
                    }
                    catch (Exception e)
                    {
                        Util.Log("Error - Get _price at MakeStitchTableFile(page: " + page.ToString() + " // priceID: " + _priceID + " // lineID: " + StitchCounter.ToString() + "): " + e.Message + " " + e.StackTrace.ToString());
                    }
                    if (!hasStitchFileTableHeader) //make sure only set one time; due to continues write to file
                    {
                        line = "ID" + "\t";
                        line += "SKU" + "\t";
                        line += "TITLE" + "\t";
                        line += "COLOR" + "\t";
                        line += "SIZE" + "\t";
                        line += "MATERIAL" + "\t";
                        line += "UPC" + "\t";
                        line += "WEIGHT" + "\t";
                        line += "PRICE" + "\t";
                        Util.WriteStitchTable(line);
                        hasStitchFileTableHeader = true;
                    }

                    line = StitchCounter.ToString() + "\t";
                    line += _sku + "\t";
                    line += _auto_description + "\t";
                    line += _color + "\t";
                    line += _size + "\t";
                    line += _material + "\t";
                    line += _upc + "\t";
                    line += _weight + "\t";
                    line += _price;
                    Util.WriteStitchTable(line);
                    Util.Log("WriteStitchTable() " + Util.LineNum(StitchCounter));
                    i++;

                }//foreach variant

                Util.Log("Success: " + (i - 1 - err).ToString() + " - Error: " + err.ToString());
            }
            catch (Exception ex)
            {
                Util.Log("Error - MakeStitchTableFile(): " + ex.Message + " " + ex.StackTrace.ToString());
            }
        }

        public static void MakeStitchTableFile_Test()
        {
            Int32 i = 1;
            Int32 err = 0;
            try
            {
                //load XML - here we don't want to link to the file due to XML format limitations
                Util.Log("Program.GetProductList()");
                Shopify.GetProductList();
                xmlData = Program.xmlData;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlData); //load actual data from string
                Util.Log("Stitch.MakeStitchTableFile_Test()");
                Util.Log("Total variants count from xmlData: " + doc.GetElementsByTagName("variant").Count.ToString());
                XmlNodeList productList = doc.GetElementsByTagName("product");
                XmlNodeList variantList;
                Util.Log("Total products count from xmlData: " + productList.Count.ToString());
                string _auto_description = string.Empty;
                string _options = string.Empty;
                string _color = string.Empty;
                string _size = string.Empty;
                string _material = string.Empty;
                string _price = string.Empty;
                string _sku = string.Empty;
                string _upc = string.Empty;
                string _weight = string.Empty;
                bool hasHeader = false;
                line = string.Empty; //reset
                //delete prior run file

                if (File.Exists(stitch_table_file)) { File.Delete(stitch_table_file); }

                foreach (XmlNode product in productList)
                {
                    _auto_description = product.SelectSingleNode("title").InnerText;
                    variantList = product.SelectNodes("variant");
                    foreach (XmlNode variant in variantList)
                    {
                        _color = Util.ParseXmlNode(variant.SelectSingleNode("option1"));
                        _size = Util.ParseXmlNode(variant.SelectSingleNode("option2"));
                        _material = Util.ParseXmlNode(variant.SelectSingleNode("option3"));
                        _sku = Util.ParseXmlNode(variant.SelectSingleNode("sku"));
                        _upc = Util.ParseXmlNode(variant.SelectSingleNode("barcode"));
                        _weight = Util.ParseXmlNode(variant.SelectSingleNode("grams"));
                        _price = Util.ParseXmlNode(variant.SelectSingleNode("price"));

                        if (!hasHeader)
                        {
                            line = "ID" + "\t";
                            line += "SKU" + "\t";
                            line += "TITLE" + "\t";
                            line += "COLOR" + "\t";
                            line += "SIZE" + "\t";
                            line += "MATERIAL" + "\t";
                            line += "UPC" + "\t";
                            line += "WEIGHT" + "\t";
                            line += "PRICE" + "\t";
                            Util.WriteStitchTable(line);
                            hasHeader = true;
                        }

                        line = i.ToString() + "\t";
                        line += _sku + "\t";
                        line += _auto_description + "\t";
                        line += _color + "\t";
                        line += _size + "\t";
                        line += _material + "\t";
                        line += _upc + "\t";
                        line += _weight + "\t";
                        line += _price;
                        Util.WriteStitchTable(line);
                        Console.WriteLine("- WriteStitchTable: " + Util.LineNum(i));
                        i++;
                    }//foreach variantList
                }//foreach product
                Util.Log("Successful written to Stitch_Table_File: " + (i - 1 - err).ToString() + " - Error: " + err.ToString());
            }
            catch (Exception ex)
            {
                Util.Log("Error - ReformatProducts(): " + ex.Message + " " + ex.StackTrace.ToString());
            }
        }

        private static string ParseOption(string description, Int32 index)
        {
            string option = string.Empty;
            try
            {
                //truncate item title at '('
                string[] raw = description.Split('(');
                //remove closing ')'
                string temp = raw[1].Replace(")", "").Trim();
                //split into pairs by delimiter
                string[] options = temp.Split(',');
                string[] value = options[index].Split(':');
                option = value[1].ToString();
                if (string.IsNullOrEmpty(option))
                {
                    return "_err";
                }
                if (index == 0 || index ==2)
                {
                    option = Util.ProperCase(option);
                }
                return option.Trim();
            }
            catch (Exception ex)
            {
                Util.Log(ex.Message);
                return "_err";
            }
        }

        public static void PlotStitchTable()
        {            
            if (dtStitchTable.Rows.Count != 0)
            {
                for (int x = 0; x < dtStitchTable.Rows.Count; x++)
                {
                    line = string.Empty;

                    for (int j = 0; j < dtStitchTable.Columns.Count - 1; j++)
                    {
                        line += dtStitchTable.Columns[j].ColumnName + "=" + dtStitchTable.Rows[x][j].ToString() + "\t";
                    }
                    Console.WriteLine(line);
                }
            }
        }

        public static void PlotStitchTableUnmatched()
        {
            if (dtStitchTable.Rows.Count != 0)
            {
                for (int x = 0; x < dtStitchTable.Rows.Count; x++)
                {
                    if (dtStitchTable.Rows[x]["MATCHED"].ToString() == "0")
                    {
                        Util.Log("Stitch Table Unmatched: "
                            + dtStitchTable.Rows[x]["ID"].ToString() + "|"
                            + dtStitchTable.Rows[x]["SKU"].ToString() + "|"
                            + dtStitchTable.Rows[x]["TITLE"].ToString() + "|"
                            + dtStitchTable.Rows[x]["COLOR"].ToString() + "|"
                            + dtStitchTable.Rows[x]["SIZE"].ToString() + "|"
                            + dtStitchTable.Rows[x]["MATERIAL"].ToString()); 
                    }
                }
            }
        }

        private static string StitchDataGet(string _postString)
        {
            try
            {
                // Create a request for the URL. 
                post_location = ConfigurationManager.AppSettings.Get("STITCH_VariantsGetLocation");
                // create an HttpWebRequest object to communicate with Shopify web service API
                HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(post_location);
                objRequest.Headers.Add("access_token", ConfigurationManager.AppSettings.Get("STITCH_AccessToken"));
                objRequest.Proxy = DefaultWebProxy;
                objRequest.Method = "POST";
                objRequest.ContentLength = _postString.Length;
                objRequest.ContentType = "application/json";
                // post data is sent as a stream
                StreamWriter myWriter = null;
                myWriter = new StreamWriter(objRequest.GetRequestStream());
                myWriter.Write(_postString);
                myWriter.Close();

                // returned values are returned as a stream, then read into a String              
                HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                {
                    post_response = responseStream.ReadToEnd();
                    responseStream.Close();
                }
                objResponse.Close();

                if (post_response.Contains("error"))
                {
                    Util.Log(post_response);
                    return "FAILURE";
                }
                xmlData += post_response; //append data string
                return "POSTSUCCESS";

            }
            catch (Exception ex)
            {
                Util.Log(": ERROR in DataGet(): " + ex.Message);
                return ex.Message;
            }
        }
        
        public static void UpdateProductsViaStitch()
        {
            //https://help.shopify.com/api/reference/product
            //https://help.shopify.com/api/reference/product_variant
            //https://schutzonline.myshopify.com/admin/variants/29678925251.json
            Int32 i = 1;
            Int32 _err = 0;
            try
            {
                Console.WriteLine("File to parse: " + Program.get_file_name); //keep copy as file
                Program.product_log_file = ConfigurationManager.AppSettings.Get("ProductsSingleFile"); 
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(Program.xmlProducts); //load product ID list
                XmlNodeList products = doc.GetElementsByTagName("id");
                foreach (XmlNode id in products)
                {
                    Program.post_location = string.Format(ConfigurationManager.AppSettings.Get(Program.STORE + "_SingleProductGetLocation"), id.InnerText);
                    xmlData = Program.DataGet(string.Empty);
                    Util.Log(Util.LineNum(i) + " GET: " + Program.post_location);
                    Console.WriteLine(Util.LineNum(i) + " " + xmlData);
                    Util.WriteProductsSingleXML(xmlData, id.InnerText);
                    i++;
                }
                return;
                XmlNodeList variants;
                XmlNodeList options;
                string skuList = string.Empty;
                Console.WriteLine("products count: " + products.Count.ToString());
                string postJSON = string.Empty;
                string ProductID = string.Empty;
                string VariantID = string.Empty;
                string Title = string.Empty;
                string SKU = string.Empty;
                string temp = string.Empty;
                string TitleInsert = string.Empty;
                string ShopifyTitle = string.Empty;
                bool skipDiscountItem = false;
                post_location = ConfigurationManager.AppSettings.Get(Program.STORE + "_ProductsPutLocation");

                foreach (XmlNode product in products)
                {                    
                    variants = product.SelectNodes("variant");
                    options = product.SelectNodes("option");
                    ProductID = product.SelectSingleNode("id").InnerText.Trim();
                    ShopifyTitle = product.SelectSingleNode("title").InnerText.Trim();
                    //track new title name; not yet known by reading store XML
                    TitleInsert = string.Empty;
                    skipDiscountItem = false;
                    if (ShopifyTitle.Contains("%")) 
                    { 
                        TitleInsert = ShopifyTitle; //do not update title
                        skipDiscountItem = true; 
                    }
 
                    //format product URL per location and log for easy review in browser; remove .json 
                    Program.product_put_location = string.Format(post_location, ProductID).Replace(".json", "");
                    Util.Log(Util.LineNum(i) + Program.product_put_location + " - SKU per Product count: " + variants.Count.ToString());
                    
                    checksum_SKU_per_product = 0;

                    string jsonPacket = "{\"product\":{";
                    jsonPacket += Util.MakeJSONField(ProductID, "id");
                    jsonPacket += Util.MakeJSONField("@TitleInsert", "title");
                    jsonPacket += "\"variants\":[";
                    foreach (XmlNode variant in variants)
                    {
                        SKU = variant.SelectSingleNode("sku").InnerText.Trim();
                        VariantID = variant.SelectSingleNode("id").InnerText.Trim();
                        //test each SKU against StitchTable
                        DataRow[] item = dtStitchTable.Select("sku = '" + SKU + "'");
                        if (item.Length > 0) //check we have rows
                        {
                            //get first instance of TITLE
                            if(string.IsNullOrEmpty(TitleInsert))
                            {
                                TitleInsert = item[0]["TITLE"].ToString().Trim();
                            }
                            jsonPacket += "{";
                            jsonPacket += Util.MakeJSONField(VariantID, "id");
                            jsonPacket += Util.MakeJSONField(ProductID, "product-id");
                            if(!skipDiscountItem)
                            {
                                //do not update price for discounted items
                                jsonPacket += Util.MakeJSONField(item[0]["PRICE"].ToString().Trim(), "price");
                            }
                            jsonPacket += Util.MakeJSONField(item[0]["COLOR"].ToString().Trim(), "option1");
                            jsonPacket += Util.MakeJSONField(item[0]["SIZE"].ToString().Trim(), "option2");
                            jsonPacket += Util.MakeJSONField(item[0]["MATERIAL"].ToString().Trim(), "option3");
                            jsonPacket += Util.MakeJSONField(item[0]["UPC"].ToString().Trim(), "barcode");                                                   
                            jsonPacket += "},";
                            checksum_SKU_per_product++;
                            item[0]["MATCHED"] = 1; //mark
                        }
                        if (item.Length > 1) //check we have unique rows
                        {
                            Util.Log(Util.LineNum(i) + Program.product_put_location
                                + " - SKU not unique: " + ShopifyTitle);
                        }
                    }//foreach variant
                    temp = jsonPacket.TrimEnd(',').Replace("@TitleInsert", TitleInsert);
                    jsonPacket = temp;
                    postJSON += "]"; //close variants
                    jsonPacket += ",";
                    //jsonPacket += "\"options\":[";

                    jsonPacket += "}";//close product
                    jsonPacket += "}";//close envelope
                    if (checksum_SKU_per_product != variants.Count)
                    {
                        Util.Log(Util.LineNum(i) + Program.product_put_location
                            + " - SKIPPED - checksum no match: "
                            + checksum_SKU_per_product.ToString()
                            + "/"
                            + variants.Count.ToString());
                        _err++;
                        continue;
                    }
                    Program.product_put_location += ".json"; //add back in for data post
                    Util.Log(Util.LineNum(i) + Program.product_put_location + " PUT_DATA_PACKET: " + jsonPacket);

                    if (Program.AppMode.ToLower() == "prod")
                    {
                        string result = Program.DataPost(Program.product_put_location, postJSON);
                        if (result != "POSTSUCCESS")//abort & review logfile
                        {
                            return;
                        }
                    }
                    i++;
                }//foreach product
                Util.Log("Success: " + (i - 1 - _err).ToString() + " - Error: " + _err.ToString());
            }
            catch (Exception ex)
            {
                Util.Log("Error - UpdateProductsViaStitch(): " + ex.Message + " " + ex.StackTrace.ToString());
            }
        }

        public static void CrossRefOptions()
        {
            //https://help.shopify.com/api/reference/product
            //https://help.shopify.com/api/reference/product_variant
            //https://schutzonline.myshopify.com/admin/variants/29678925251.json
            Int32 i = 1;
            Int32 _err = 0;
            try
            {
                Console.WriteLine("File to parse: " + Program.get_file_name); //keep copy as file
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlData); //load actual data from memory
                XmlNodeList products = doc.GetElementsByTagName("product");
                XmlNodeList variants;
                string skuList = string.Empty;
                Console.WriteLine("products count: " + products.Count.ToString());
                string postJSON = string.Empty;
                string ProductID = string.Empty;
                string SKU = string.Empty;
                string Option1 = string.Empty;
                string Option2 = string.Empty;               
                string Option3 = string.Empty;
                //add some extra columns for tracing
                dtStitchTable.Columns.Add("PRODUCTID");
                dtStitchTable.Columns.Add("OPTION1");
                dtStitchTable.Columns.Add("OPTION2");
                dtStitchTable.Columns.Add("OPTION3");
                Int32 row = 1;
                foreach (XmlNode product in products)
                {
                    variants = product.SelectNodes("variant");
                    ProductID = product.SelectSingleNode("id").InnerText.Trim();
                    //format product URL per location and log for easy review in browser; remove .json 
                    Program.product_put_location = string.Format(post_location, ProductID).Replace(".json", "");
                    Util.Log(Util.LineNum(i) + Program.product_put_location + " - SKU per Product count: " + variants.Count.ToString());

                    foreach (XmlNode variant in variants)
                    {
                        SKU = variant.SelectSingleNode("sku").InnerText.Trim();                       
                        Option1 = variant.SelectSingleNode("option1").InnerText.Trim();
                        Option2 = string.Empty;
                        if (variant.SelectSingleNode("option2") != null)
                        { 
                            Option2 = variant.SelectSingleNode("option2").InnerText.Trim();
                        }
                        Option3 = string.Empty;
                        if (variant.SelectSingleNode("option3") != null)
                        {
                            Option3 = variant.SelectSingleNode("option3").InnerText.Trim();
                        }
                        //test each SKU against StitchTable
                        DataRow[] item = dtStitchTable.Select("sku = '" + SKU + "'");
                        if (item.Length > 0) //check we have rows
                        {
                            item[0]["PRODUCTID"] = ProductID;
                            item[0]["OPTION1"] = Option1;
                            item[0]["OPTION2"] = Option2;
                            item[0]["OPTION3"] = Option3;
                        }
                        Console.WriteLine(Util.LineNum(row) + " OPTION1: " + Option1 + "\t OPTION2: " + Option2 + "\t OPTION3: " + Option3);
                        row++;
                    }//foreach variant
                    i++;                    
                }//foreach product

                string logFile = ConfigurationManager.AppSettings.Get("CrossRefFile");
                logFile += "options_cross_ref_" + Program.STORE + " _" + Program.TICKS + ".xml";

                for (int x = 0; x < dtStitchTable.Rows.Count; x++)
                {
                    line = string.Empty;

                    for (int j = 0; j < dtStitchTable.Columns.Count - 1; j++)
                    {
                        line += dtStitchTable.Columns[j].ColumnName + "=" + dtStitchTable.Rows[x][j].ToString() + "\t";
                    }
                    Console.WriteLine(Util.LineNum(x+1) + " " + line);
                    using (StreamWriter swLog = File.AppendText(logFile))
                    {
                        swLog.WriteLine(line);
                        swLog.Flush();
                        swLog.Close();
                    }
                }
                Util.Log("Cross ref count: " + (i - 1 - _err).ToString() + " - Error: " + _err.ToString());
            }
            catch (Exception ex)
            {
                Util.Log("Error - UpdateProductsViaStitch(): " + ex.Message + " " + ex.StackTrace.ToString());
            }
        }
    }
}
