using BitMEX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BitMexSampleBot
{
    public partial class Form1 : Form
    {

        // IMPORTANT - Enter your API Key information below

        //TEST NET - NEW
        //private static string TestbitmexKey = "YOURHEREKEYHERE";
        //private static string TestbitmexSecret = "YOURSECRETHERE";
        private static string TestbitmexDomain = "https://testnet.bitmex.com";

        //REAL NET
        //private static string bitmexKey = "YOURHEREKEYHERE";
        //private static string bitmexSecret = "YOURSECRETHERE";
        private static string bitmexDomain = "https://www.bitmex.com";


        BitMEXApi bitmex;
        List<OrderBook> CurrentBook = new List<OrderBook>();
        List<Instrument> ActiveInstruments = new List<Instrument>();
        Instrument ActiveInstrument = new Instrument();
        List<Candle> Candles = new List<Candle>();

        bool Running = false;
        string Mode = "Wait";
        List<Position> OpenPositions = new List<Position>();
        List<Order> OpenOrders = new List<Order>();

        // For BBand Indicator Info, 20, close 2
        int BBLength = 20;
        double BBMultiplier = 2;

        // For EMA Indicator Periods, also used in MACD
        int EMA1Period = 26;  // Slow MACD EMA
        int EMA2Period = 12;  // Fast MACD EMA
        int EMA3Period = 9;   

        // For MACD
        int MACDEMAPeriod = 9;  // MACD smoothing period

        // For checking API validity before attempting orders/account specific moves
        bool APIValid = false;
        double WalletBalance = 0;

        // For ATR
        int ATR1Period = 7;
        int ATR2Period = 20;

        // For Over Time
        int OTContractsPer = 0;
        int OTIntervalSeconds = 0;
        int OTIntervalCount = 0;
        int OTTimerCount = 0;
        string OTSide = "Buy";

        // NEW - For RSI
        int RSIPeriod = 14;

        public Form1()
        {
            InitializeComponent();
            InitializeDropdownsAndSettings();
            InitializeAPI();
            InitializeCandleArea();
            InitializeOverTime();

        }
        private void InitializeDropdownsAndSettings()
        {
            ddlNetwork.SelectedIndex = 0;
            ddlOrderType.SelectedIndex = 0;
            ddlCandleTimes.SelectedIndex = 0;
            ddlAutoOrderType.SelectedIndex = 0;

            LoadAPISettings();
        }

        private void LoadAPISettings()
        {
            switch (ddlNetwork.SelectedItem.ToString())
            {
                case "TestNet":
                    txtAPIKey.Text = Properties.Settings.Default.TestAPIKey;
                    txtAPISecret.Text = Properties.Settings.Default.TestAPISecret;
                    break;
                case "RealNet":
                    txtAPIKey.Text = Properties.Settings.Default.APIKey;
                    txtAPISecret.Text = Properties.Settings.Default.APISecret;
                    break;
            }
        }

        private void InitializeOverTime() // NEW - Just updates the summary
        {
            UpdateOverTimeSummary();
        }

        private void InitializeCandleArea()
        {
            tmrCandleUpdater.Start();
        }

        private void InitializeAPI()
        {
            switch(ddlNetwork.SelectedItem.ToString())
            {
                case "TestNet":
                    bitmex = new BitMEXApi(txtAPIKey.Text, txtAPISecret.Text, TestbitmexDomain);
                    break;
                case "RealNet":
                    bitmex = new BitMEXApi(txtAPIKey.Text, txtAPISecret.Text, bitmexDomain);
                    break;
            }

            // We must do this in case symbols are different on test and real net
            GetAPIValidity(); // Validate API keys by checking and displaying account balance.
            InitializeSymbolInformation();
            
        }

        private void InitializeSymbolInformation()
        {
            ActiveInstruments = bitmex.GetActiveInstruments().OrderByDescending(a => a.Volume24H).ToList();
            ddlSymbol.DataSource = ActiveInstruments;
            ddlSymbol.DisplayMember = "Symbol";
            ddlSymbol.SelectedIndex = 0;
            ActiveInstrument = ActiveInstruments[0];
        }

        private double CalculateMakerOrderPrice(string Side)
        {
            CurrentBook = bitmex.GetOrderBook(ActiveInstrument.Symbol, 1);

            double SellPrice = CurrentBook.Where(a => a.Side == "Sell").FirstOrDefault().Price;
            double BuyPrice = CurrentBook.Where(a => a.Side == "Buy").FirstOrDefault().Price;

            double OrderPrice = 0;

            switch (Side)
            {
                case "Buy":
                    OrderPrice = BuyPrice;

                    if (BuyPrice + ActiveInstrument.TickSize >= SellPrice)
                    {
                        OrderPrice = BuyPrice;
                    }
                    else if (BuyPrice + ActiveInstrument.TickSize < SellPrice)
                    {
                        OrderPrice = BuyPrice + ActiveInstrument.TickSize;
                    }
                    break;
                case "Sell":
                    OrderPrice = SellPrice;

                    if (SellPrice - ActiveInstrument.TickSize <= BuyPrice)
                    {
                        OrderPrice = SellPrice;
                    }
                    else if (SellPrice - ActiveInstrument.TickSize > BuyPrice)
                    {
                        OrderPrice = SellPrice - ActiveInstrument.TickSize;
                    }
                    break;
            }
            return OrderPrice;
        }

        private void MakeOrder(string Side, int Qty, double Price = 0)
        {
            if (chkCancelWhileOrdering.Checked)
            {
                bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
            }
            switch(ddlOrderType.SelectedItem)
            {
                case "Limit Post Only":
                    if (Price == 0)
                    {
                        Price = CalculateMakerOrderPrice(Side);
                    }
                    var MakerBuy = bitmex.PostOrderPostOnly(ActiveInstrument.Symbol, Side, Price, Qty);
                    break;
                case "Market":
                    bitmex.MarketOrder(ActiveInstrument.Symbol, Side, Qty);
                    break;
            }
        }

        private void AutoMakeOrder(string Side, int Qty, double Price = 0)
        {
            switch (ddlAutoOrderType.SelectedItem)
            {
                case "Limit Post Only":
                    if (Price == 0)
                    {
                        Price = CalculateMakerOrderPrice(Side);
                    }
                    var MakerBuy = bitmex.PostOrderPostOnly(ActiveInstrument.Symbol, Side, Price, Qty);
                    break;
                case "Market":
                    bitmex.MarketOrder(ActiveInstrument.Symbol, Side, Qty);
                    break;
            }
        }

        private void btnBuy_Click(object sender, EventArgs e)
        {
            MakeOrder("Buy", Convert.ToInt32(nudQty.Value));
        }

        private void btnSell_Click(object sender, EventArgs e)
        {
            MakeOrder("Sell", Convert.ToInt32(nudQty.Value));
        }

        private void btnCancelOpenOrders_Click(object sender, EventArgs e)
        {
            bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
        }

        private void ddlNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAPISettings();
            InitializeAPI();
        }

        private void ddlSymbol_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActiveInstrument = bitmex.GetInstrument(((Instrument)ddlSymbol.SelectedItem).Symbol)[0];
        }

        private void UpdateCandles()
        {
            // Get candles
            Candles = bitmex.GetCandleHistory(ActiveInstrument.Symbol, 500, ddlCandleTimes.SelectedItem.ToString());

            Candles = Candles.OrderBy(a => a.TimeStamp).ToList();

            // Set Indicator Info
            foreach (Candle c in Candles)
            {
                c.PCC = Candles.Where(a => a.TimeStamp < c.TimeStamp).Count();

                int MA1Period = Convert.ToInt32(nudMA1.Value);
                int MA2Period = Convert.ToInt32(nudMA2.Value);

                if (c.PCC >= MA1Period)
                {
                    // Get the moving average over the last X periods using closing -- INCLUDES CURRENT CANDLE <=
                    c.MA1 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(MA1Period).Average(a => a.Close);
                } // With not enough candles, we don't set to 0, we leave it null.

                if (c.PCC >= MA2Period)
                {
                    // Get the moving average over the last X periods using closing -- INCLUDES CURRENT CANDLE <=
                    c.MA2 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(MA2Period).Average(a => a.Close);
                } // With not enough candles, we don't set to 0, we leave it null.

                if (c.PCC >= BBLength) // Bollinger Bands
                {
                    // BBand calculation available on trading view wiki: https://www.tradingview.com/wiki/Bollinger_Bands_(BB)
                    // You might need to also google how to calculate standard deviation as well: https://stackoverflow.com/questions/14635735/how-to-efficiently-calculate-a-moving-standard-deviation

                    // BBMiddle is just 20 period moving average
                    c.BBMiddle = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(BBLength).Average(a => a.Close);

                    // Calculating the std deviation is important, and the hard part.
                    double total_squared = 0;
                    double total_for_average = Convert.ToDouble(Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(BBLength).Sum(a => a.Close));
                    foreach (Candle cb in Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(BBLength).ToList())
                    {
                        total_squared += Math.Pow(Convert.ToDouble(cb.Close), 2);
                    }
                    double stdev = Math.Sqrt((total_squared - Math.Pow(total_for_average, 2) / BBLength) / BBLength);
                    c.BBUpper = c.BBMiddle + (stdev * BBMultiplier);
                    c.BBLower = c.BBMiddle - (stdev * BBMultiplier);
                }


                // EMA
                if (c.PCC >= EMA1Period)
                {
                    double p1 = EMA1Period + 1;
                    double EMAMultiplier = Convert.ToDouble(2 / p1);

                    if(c.PCC == EMA1Period)
                    {
                        // This is our seed EMA, using SMA of EMA1 Period for EMA 1
                        c.EMA1 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(EMA1Period).Average(a => a.Close);
                    }
                    else
                    {
                        double? LastEMA = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().EMA1;
                        c.EMA1 =((c.Close - LastEMA) * EMAMultiplier) + LastEMA;
                    }
                }

                if (c.PCC >= EMA2Period)
                {
                    double p1 = EMA2Period + 1;
                    double EMAMultiplier = Convert.ToDouble(2 / p1);

                    if (c.PCC == EMA2Period)
                    {
                        // This is our seed EMA, using SMA
                        c.EMA2 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(EMA2Period).Average(a => a.Close);
                    }
                    else
                    {
                        double? LastEMA = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().EMA2;
                        c.EMA2 = ((c.Close - LastEMA) * EMAMultiplier) + LastEMA;
                    }
                }

                if (c.PCC >= EMA3Period)
                {
                    double p1 = EMA3Period + 1;
                    double EMAMultiplier = Convert.ToDouble(2 / p1);

                    if (c.PCC == EMA3Period)
                    {
                        // This is our seed EMA, using SMA
                        c.EMA3 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(EMA3Period).Average(a => a.Close);
                    }
                    else
                    {
                        double? LastEMA = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().EMA3;
                        c.EMA3 = ((c.Close - LastEMA) * EMAMultiplier) + LastEMA;
                    }
                }

                // MACD
                // We can only do this if we have the longest EMA period, EMA1
                if(c.PCC >= EMA1Period)
                {

                    double p1 = MACDEMAPeriod + 1;
                    double MACDEMAMultiplier = Convert.ToDouble(2 / p1);

                    c.MACDLine = (c.EMA2 - c.EMA1); // default is 12EMA - 26EMA
                    if(c.PCC == EMA1Period + MACDEMAPeriod - 1)
                    {
                        // Set this to SMA of MACDLine to seed it
                        c.MACDSignalLine = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(MACDEMAPeriod).Average(a => (a.MACDLine));
                    }
                    else if (c.PCC > EMA1Period + MACDEMAPeriod - 1)
                    {
                        // We can calculate this EMA based off past candle EMAs
                        double? LastMACDSignalLine = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().MACDSignalLine;
                        c.MACDSignalLine = ((c.MACDLine - LastMACDSignalLine) * MACDEMAMultiplier) + LastMACDSignalLine;
                    }
                    c.MACDHistorgram = c.MACDLine - c.MACDSignalLine;
                }

                // ATR, setting TR
                if(c.PCC == 0)
                {
                    c.SetTR(c.High);
                }
                else if(c.PCC > 0)
                {
                    c.SetTR(Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().Close);
                }

                // Setting ATRs
                if(c.PCC == ATR1Period - 1)
                {
                    c.ATR1 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(ATR1Period).Average(a => a.TR);
                }
                else if(c.PCC > ATR1Period - 1)
                {
                    double p1 = ATR1Period + 1;
                    double ATR1Multiplier = Convert.ToDouble(2 / p1);
                    double? LastATR1 = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().ATR1;
                    c.ATR1 = ((c.TR - LastATR1) * ATR1Multiplier) + LastATR1;
                }

                if (c.PCC == ATR2Period - 1)
                {
                    c.ATR2 = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(ATR2Period).Average(a => a.TR);
                }
                else if (c.PCC > ATR2Period - 1)
                {
                    double p1 = ATR2Period + 1;
                    double ATR2Multiplier = Convert.ToDouble(2 / p1);
                    double? LastATR2 = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().ATR2;
                    c.ATR2 = ((c.TR - LastATR2) * ATR2Multiplier) + LastATR2;
                }

                // NEW - For RSI
                if(c.PCC == RSIPeriod - 1)
                {
                    // AVG Gain is average of just gains, for all periods, (14), not just periods with gains.  Same goes for losses but with losses.
                    c.AVGGain = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Where(a => a.GainOrLoss > 0).Take(RSIPeriod).Sum(a => a.GainOrLoss) / RSIPeriod;
                    c.AVGLoss = (Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Where(a => a.GainOrLoss < 0).Take(RSIPeriod).Sum(a => a.GainOrLoss) / RSIPeriod) * -1;

                    c.RS = c.AVGGain / c.AVGLoss; // Only like this on first one (seeding it)
                    c.RSI = 100 - (100 / (1 + c.RS));
                }
                else if (c.PCC > RSIPeriod - 1)
                {
                    // AVG Gain is average of just gains, for all periods, (14), not just periods with gains.  Same goes for losses but with losses.
                    c.AVGGain = Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Where(a => a.GainOrLoss > 0).Take(RSIPeriod).Sum(a => a.GainOrLoss) / RSIPeriod;
                    c.AVGLoss = (Candles.Where(a => a.TimeStamp <= c.TimeStamp).OrderByDescending(a => a.TimeStamp).Where(a => a.GainOrLoss < 0).Take(RSIPeriod).Sum(a => a.GainOrLoss) / RSIPeriod) * -1;

                    double? LastAVGGain = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().AVGGain;
                    double? LastAVGLoss = Candles.Where(a => a.TimeStamp < c.TimeStamp).OrderByDescending(a => a.TimeStamp).Take(1).FirstOrDefault().AVGLoss;
                    double? Gain = 0;
                    double? Loss = 0;

                    if(c.GainOrLoss > 0)
                    {
                        Gain = c.GainOrLoss;
                    }
                    else if (c.GainOrLoss < 0)
                    {
                        Loss = c.GainOrLoss;
                    }

                    c.RS = (((LastAVGGain * (RSIPeriod - 1)) + Gain) / RSIPeriod) / (((LastAVGLoss * (RSIPeriod - 1)) + Loss) / RSIPeriod);
                    c.RSI = 100 - (100 / (1 + c.RS));
                }

            }

            Candles = Candles.OrderByDescending(a => a.TimeStamp).ToList();

            // Show Candles
            dgvCandles.DataSource = Candles;

            // This is where we are going to determine the "mode" of the bot based on MAs, trades happen on another timer
            if(Running)//We could set this up to also ignore setting bot mode if we've already reviewed current candles
                            //  However, if you wanted to use info from the most current candle, that wouldn't work well
            {
                SetBotMode();  // We really only need to set bot mode if the bot is running
                btnAutomatedTrading.Text = "Stop - " + Mode;// so we can see what the mode of the bot is while running
            }
        }

        private void SetBotMode()
        {
            // This is where we are going to determine what mode the bot is in
            if(rdoBuy.Checked)
            {
                //if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 <= Candles[2].MA2)) // Most recently closed candle crossed over up
                //{
                //    // Did the last full candle have MA1 cross above MA2?  We'll need to buy now.
                //    Mode = "Buy";
                //}
                //else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 >= Candles[2].MA2))
                //{
                //    // Did the last full candle have MA1 cross below MA2?  We'll need to close any open position.
                //    Mode = "CloseAndWait";
                //}
                //else if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 > Candles[2].MA2))
                //{
                //    // If no crossover, is MA1 still above MA2? We'll need to leave our position open.
                //    Mode = "Wait";
                //}
                //else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 < Candles[2].MA2))
                //{
                //    // If no crossover, is MA1 still below MA2? We'll need to make sure we don't have a position open.
                //    Mode = "CloseAndWait";
                //}

                // MACD Example
                if ((Candles[1].MACDLine > Candles[1].MACDSignalLine) && (Candles[2].MACDLine <= Candles[2].MACDSignalLine)) // Most recently closed candle crossed over up
                {
                    // Did the last full candle have MACDLine cross above MACDSignalLine?  We'll need to buy now.
                    Mode = "Buy";
                }
                else if ((Candles[1].MACDLine < Candles[1].MACDSignalLine) && (Candles[2].MACDLine >= Candles[2].MACDSignalLine))
                {
                    // Did the last full candle have MACDLine cross below MACDSignalLine?  We'll need to close any open position.
                    Mode = "CloseAndWait";
                }
                else if ((Candles[1].MACDLine > Candles[1].MACDSignalLine) && (Candles[2].MACDLine > Candles[2].MACDSignalLine))
                {
                    // If no crossover, is MACDLine still above MACDSignalLine? We'll need to leave our position open.
                    Mode = "Wait";
                }
                else if ((Candles[1].MACDLine < Candles[1].MACDSignalLine) && (Candles[2].MACDLine < Candles[2].MACDSignalLine))
                {
                    // If no crossover, is MACDLine still below MACDSignalLine? We'll need to make sure we don't have a position open.
                    Mode = "CloseAndWait";
                }

            }
            else if(rdoSell.Checked)
            {
                if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 <= Candles[2].MA2)) // Most recently closed candle crossed over up
                {
                    // Did the last full candle have MA1 cross above MA2?  We'll need to close any open position.
                    Mode = "CloseAndWait";
                }
                else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 >= Candles[2].MA2))
                {
                    // Did the last full candle have MA1 cross below MA2?  We'll need to sell now.
                    Mode = "Sell";
                }
                else if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 > Candles[2].MA2))
                {
                    // If no crossover, is MA1 still above MA2? We'll need to make sure we don't have a position open.
                    Mode = "CloseAndWait";
                }
                else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 < Candles[2].MA2))
                {
                    // If no crossover, is MA1 still below MA2? We'll need to leave our position open.
                    Mode = "Wait";
                }
            }
            else if(rdoSwitch.Checked)
            {
                //NEW
                if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 <= Candles[2].MA2)) // Most recently closed candle crossed over up
                {
                    // Did the last full candle have MA1 cross above MA2?  Triggers a buy in switch setting.
                    Mode = "Buy";
                }
                else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 >= Candles[2].MA2))
                {
                    // Did the last full candle have MA1 cross below MA2?  Triggers a sell in switch setting
                    Mode = "Sell";
                }
                else if ((Candles[1].MA1 > Candles[1].MA2) && (Candles[2].MA1 > Candles[2].MA2))
                {
                    // If no crossover, is MA1 still above MA2? Keep long position open, close any shorts if they are still open.
                    Mode = "CloseShortsAndWait";
                }
                else if ((Candles[1].MA1 < Candles[1].MA2) && (Candles[2].MA1 < Candles[2].MA2))
                {
                    // If no crossover, is MA1 still below MA2? Keep short position open, close any longs if they are still open.
                    Mode = "CloseLongsAndWait";
                }
            }
        }

        private void tmrCandleUpdater_Tick(object sender, EventArgs e)
        {
            if(chkUpdateCandles.Checked)
            {
                UpdateCandles();
            }
            
        }

        private void chkUpdateCandles_CheckedChanged(object sender, EventArgs e)
        {
            if(chkUpdateCandles.Checked)
            {
                tmrCandleUpdater.Start();
            }
            else
            {
                tmrCandleUpdater.Stop();
            }
        }

        private void ddlCandleTimes_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCandles();
        }

        private void btnAutomatedTrading_Click(object sender, EventArgs e)
        {
            if(btnAutomatedTrading.Text == "Start")
            {
                tmrAutoTradeExecution.Start();
                btnAutomatedTrading.Text = "Stop - " + Mode;
                btnAutomatedTrading.BackColor = Color.Red;
                Running = true;
                rdoBuy.Enabled = false;
                rdoSell.Enabled = false;
                rdoSwitch.Enabled = false;
            }
            else
            {
                tmrAutoTradeExecution.Stop();
                btnAutomatedTrading.Text = "Start";
                btnAutomatedTrading.BackColor = Color.LightGreen;
                Running = false;
                rdoBuy.Enabled = true;
                rdoSell.Enabled = true;
                rdoSwitch.Enabled = true; 
            }
            
        }

        private void tmrAutoTradeExecution_Tick(object sender, EventArgs e)
        {
            OpenPositions = bitmex.GetOpenPositions(ActiveInstrument.Symbol);
            OpenOrders = bitmex.GetOpenOrders(ActiveInstrument.Symbol);

            if(chkAutoMarketTakeProfits.Checked && OpenPositions.Any() && Mode != "Sell" && Mode != "Buy") // See if we are taking profits on open positions, and have positions open and we aren't in our buy or sell periods
            {
                lblAutoUnrealizedROEPercent.Text = Math.Round((Convert.ToDouble(OpenPositions[0].UnrealisedRoePcnt * 100)), 2).ToString();
                // Did we meet our profit threshold yet?
                if (Convert.ToDouble(OpenPositions[0].UnrealisedRoePcnt * 100) >= Convert.ToDouble(nudAutoMarketTakeProfitPercent.Value))
                {
                    // Make a market order to close out the position, also cancel all orders so nothing else fills if we had unfilled limit orders still open.
                    string Side = "Sell";
                    int Quantity = 0;
                    if(OpenPositions[0].CurrentQty > 0)
                    {
                        Side = "Sell";
                        Quantity = Convert.ToInt32(OpenPositions[0].CurrentQty);
                    }
                    else if (OpenPositions[0].CurrentQty < 0)
                    {
                        Side = "Buy";
                        Quantity = Convert.ToInt32(OpenPositions[0].CurrentQty) * -1;
                    }
                    bitmex.MarketOrder(ActiveInstrument.Symbol, Side, Quantity);

                    // Get our positions and orders again to be able to process rest of logic with new information.
                    OpenPositions = bitmex.GetOpenPositions(ActiveInstrument.Symbol);
                    OpenOrders = bitmex.GetOpenOrders(ActiveInstrument.Symbol);
                }
            }
            
            if(rdoBuy.Checked)
            {
                switch(Mode)
                {
                    case "Buy":
                        // See if we already have a position open
                        if(OpenPositions.Any())
                        {
                            // We have an open position, is it at our desired quantity?
                            if(OpenPositions[0].CurrentQty < nudAutoQuantity.Value)
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // We still have an open sell order, cancel that order, make a new buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                }
                                    
                            } // No else, it is filled to where we want.
                        }
                        else
                        {
                            if(OpenOrders.Any())
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // We still have an open sell order, cancel that order, make a new buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                }
                            }
                            else
                            {
                                AutoMakeOrder("Buy", Convert.ToInt32(nudAutoQuantity.Value));
                            }
                        }
                        break;
                    case "CloseAndWait":
                        // See if we have open positions, if so, close them
                        if(OpenPositions.Any())
                        {
                            // Now, do we have open orders?  If so, we want to make sure they are at the right price
                            if (OpenOrders.Any())
                            {
                                if(OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open buy order, cancel that order, make a new sell order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if(OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }
                                        
                            }
                            else
                            {
                                // No open orders, need to make an order to sell
                                AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                            }
                        }
                        else if(OpenOrders.Any())
                        {
                            // We don't have an open position, but we do have an open order, close that order, we don't want to open any position here.
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                    case "Wait":
                        // We are in wait mode, no new buying or selling - close open orders
                        if (OpenOrders.Any())
                        {
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                }
            }
            else if(rdoSell.Checked)
            {
                switch (Mode)
                {
                    case "Sell":
                        // See if we already have a position open
                        if (OpenPositions.Any())
                        {
                            // We have an open position, is it at our desired quantity?
                            if (OpenPositions[0].CurrentQty < nudAutoQuantity.Value)
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open Buy order, cancel that order, make a new Sell order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }

                            } // No else, it is filled to where we want.
                        }
                        else
                        {
                            if (OpenOrders.Any())
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open buy order, cancel that order, make a new sell order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }
                            }
                            else
                            {
                                AutoMakeOrder("Sell", Convert.ToInt32(nudAutoQuantity.Value));
                            }
                        }
                        break;
                    case "CloseAndWait":
                        // See if we have open positions, if so, close them
                        if (OpenPositions.Any())
                        {
                            // Now, do we have open orders?  If so, we want to make sure they are at the right price
                            if (OpenOrders.Any())
                            {
                                if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // We still have an open sell order, cancel that order, make a new buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                }

                            }
                            else
                            {
                                // No open orders, need to make an order to sell
                                AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                            }
                        }
                        else if (OpenOrders.Any())
                        {
                            // We don't have an open position, but we do have an open order, close that order, we don't want to open any position here.
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                    case "Wait":
                        // We are in wait mode, no new buying or selling - close open orders
                        if (OpenOrders.Any())
                        {
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                }
            }
            else if(rdoSwitch.Checked)
            {
                switch(Mode)
                {
                    case "Buy":
                        if (OpenPositions.Any())
                        {
                            int PositionDifference = Convert.ToInt32(nudAutoQuantity.Value - OpenPositions[0].CurrentQty);
                                
                                if (OpenOrders.Any())
                                {
                                    // If we have an open order, edit it
                                    if (OpenOrders.Any(a => a.Side == "Sell"))
                                    {
                                        // We still have an open Sell order, cancel that order, make a new Buy order
                                        string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                        AutoMakeOrder("Buy", PositionDifference);
                                    }
                                    else if (OpenOrders.Any(a => a.Side == "Buy"))
                                    {
                                        // Edit our only open order, code should not allow for more than 1 at a time for now
                                        string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                    }
                                }
                                else
                                {
                                    // No open orders, make one for the difference
                                    if(PositionDifference != 0)
                                    {
                                        AutoMakeOrder("Buy", Convert.ToInt32(PositionDifference));
                                    }
                                    
                                }
                                
                        }
                        else
                        {
                            if (OpenOrders.Any())
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // We still have an open Sell order, cancel that order, make a new Buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Buy", Convert.ToInt32(nudAutoQuantity.Value));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                }
                            }
                            else
                            {
                                AutoMakeOrder("Buy", Convert.ToInt32(nudAutoQuantity.Value));
                            }
                        }
                        break;
                    case "Sell":
                        if (OpenPositions.Any())
                        {
                            int PositionDifference = Convert.ToInt32(nudAutoQuantity.Value + OpenPositions[0].CurrentQty);

                            if (OpenOrders.Any())
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open Sell order, cancel that order, make a new Buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", PositionDifference);
                                }
                                else if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }
                            }
                            else
                            {
                                // No open orders, make one for the difference
                                if (PositionDifference != 0)
                                {
                                    AutoMakeOrder("Sell", Convert.ToInt32(PositionDifference));
                                }
                                
                            }

                        }
                        else
                        {
                            if (OpenOrders.Any())
                            {
                                // If we have an open order, edit it
                                if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open Sell order, cancel that order, make a new Buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", Convert.ToInt32(nudAutoQuantity.Value));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }
                            }
                            else
                            {
                                AutoMakeOrder("Sell", Convert.ToInt32(nudAutoQuantity.Value));
                            }
                        }
                        break;
                    case "CloseLongsAndWait":
                        if (OpenPositions.Any())
                        {
                            // Now, do we have open orders?  If so, we want to make sure they are at the right price
                            if (OpenOrders.Any())
                            {
                                if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // We still have an open buy order, cancel that order, make a new sell order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Sell"));
                                }

                            }
                            else if(OpenPositions[0].CurrentQty > 0)
                            {
                                // No open orders, need to make an order to sell
                                AutoMakeOrder("Sell", Convert.ToInt32(OpenPositions[0].CurrentQty));
                            }
                        }
                        else if (OpenOrders.Any())
                        {
                            // We don't have an open position, but we do have an open order, close that order, we don't want to open any position here.
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                    case "CloseShortsAndWait":
                        // Close any open orders, close any open shorts, we've missed our chance to long.
                        if (OpenPositions.Any())
                        {
                            // Now, do we have open orders?  If so, we want to make sure they are at the right price
                            if (OpenOrders.Any())
                            {
                                if (OpenOrders.Any(a => a.Side == "Sell"))
                                {
                                    // We still have an open sell order, cancel that order, make a new buy order
                                    string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                                    AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                                }
                                else if (OpenOrders.Any(a => a.Side == "Buy"))
                                {
                                    // Edit our only open order, code should not allow for more than 1 at a time for now
                                    string result = bitmex.EditOrderPrice(OpenOrders[0].OrderId, CalculateMakerOrderPrice("Buy"));
                                }

                            }
                            else if (OpenPositions[0].CurrentQty < 0)
                            {
                                // No open orders, need to make an order to sell
                                AutoMakeOrder("Buy", Convert.ToInt32(OpenPositions[0].CurrentQty));
                            }
                        }
                        else if (OpenOrders.Any())
                        {
                            // We don't have an open position, but we do have an open order, close that order, we don't want to open any position here.
                            string result = bitmex.CancelAllOpenOrders(ActiveInstrument.Symbol);
                        }
                        break;
                }
            }
        }

        // Check account balance/validity
        private void GetAPIValidity()
        {
            try // Code is simple, if we get our account balance without an error the API is valid, if not, it will throw an error and API will be marked not valid.
            {
                
                WalletBalance = bitmex.GetAccountBalance();
                if (WalletBalance >= 0)
                {
                    APIValid = true;
                    stsAPIValid.Text = "API keys are valid";
                    stsAccountBalance.Text = "Balance: " + WalletBalance.ToString();
                }
                else
                {
                    APIValid = false;
                    stsAPIValid.Text = "API keys are invalid";
                    stsAccountBalance.Text = "Balance: 0";
                }
            }
            catch (Exception ex)
            {
                APIValid = false;
                stsAPIValid.Text = "API keys are invalid";
                stsAccountBalance.Text = "Balance: 0";
            }
        }

        // Update balances
        private void btnAccountBalance_Click(object sender, EventArgs e)
        {
            GetAPIValidity();
        }

        // Set Market Stops
        private void btnManualSetStop_Click(object sender, EventArgs e)
        {
            OpenPositions = bitmex.GetOpenPositions(ActiveInstrument.Symbol);

            if(OpenPositions.Any()) // Only set stops if we have open positions
            {
                // Now determine what kind of stop to set
                if(OpenPositions[0].CurrentQty > 0)
                {
                    // Determine stop price, x percent below current price.
                    double PercentPriceDifference = Convert.ToDouble(Candles[0].Close) * (Convert.ToDouble(nudStopPercent.Value) / 100);
                    double StopPrice = Convert.ToDouble(Candles[0].Close) - PercentPriceDifference;
                    // Round the Stop Price down to the tick size so the price is valid
                    StopPrice = StopPrice - (StopPrice % ActiveInstrument.TickSize);
                    // Set a stop to sell
                    bitmex.MarketStop(ActiveInstrument.Symbol, "Sell", StopPrice, Convert.ToInt32(OpenPositions[0].CurrentQty), true, ddlCandleTimes.SelectedItem.ToString());
                }
                else if(OpenPositions[0].CurrentQty < 0)
                {
                    // Determine stop price, x percent below current price.
                    double PercentPriceDifference = Convert.ToDouble(Candles[0].Close) * (Convert.ToDouble(nudStopPercent.Value) / 100);
                    double StopPrice = Convert.ToDouble(Candles[0].Close) + PercentPriceDifference;
                    // Round the Stop Price down to the tick size so the price is valid
                    StopPrice = StopPrice - (StopPrice % ActiveInstrument.TickSize);
                    // Set a stop to sell
                    bitmex.MarketStop(ActiveInstrument.Symbol, "Buy", StopPrice, (Convert.ToInt32(OpenPositions[0].CurrentQty) * -1), true, ddlCandleTimes.SelectedItem.ToString());
                }
            }
        }

        private void txtAPIKey_TextChanged(object sender, EventArgs e)
        {
            switch (ddlNetwork.SelectedItem.ToString())
            {
                case "TestNet":
                    Properties.Settings.Default.TestAPIKey = txtAPIKey.Text;
                    break;
                case "RealNet":
                    Properties.Settings.Default.APIKey = txtAPIKey.Text;
                    break;
            }
            SaveSettings();
            InitializeAPI();
        }

        private void txtAPISecret_TextChanged(object sender, EventArgs e)
        {
            switch (ddlNetwork.SelectedItem.ToString())
            {
                case "TestNet":
                    Properties.Settings.Default.TestAPISecret = txtAPISecret.Text;
                    break;
                case "RealNet":
                    Properties.Settings.Default.APISecret = txtAPISecret.Text;
                    break;
            }
            SaveSettings();
            InitializeAPI();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.Save();
        }


        // NEW - Over Time ordering
        private void UpdateOverTimeSummary()
        {
            OTContractsPer = Convert.ToInt32(nudOverTimeContracts.Value);
            OTIntervalSeconds = Convert.ToInt32(nudOverTimeInterval.Value);
            OTIntervalCount = Convert.ToInt32(nudOverTimeIntervalCount.Value);

            lblOverTimeSummary.Text = (OTContractsPer * OTIntervalCount).ToString() + " Contracts over " + OTIntervalCount.ToString() + " orders during a total of " + (OTIntervalCount * OTIntervalSeconds).ToString() + " seconds.";

        }

        private void nudOverTimeContracts_ValueChanged(object sender, EventArgs e)
        {
            UpdateOverTimeSummary();
        }

        private void nudOverTimeInterval_ValueChanged(object sender, EventArgs e)
        {
            UpdateOverTimeSummary();
        }

        private void nudOverTimeIntervalCount_ValueChanged(object sender, EventArgs e)
        {
            UpdateOverTimeSummary();
        }

        private void btnBuyOverTimeOrder_Click(object sender, EventArgs e)
        {
            UpdateOverTimeSummary(); // Makes sure our variables are current.

            OTSide = "Buy";

            tmrTradeOverTime.Interval = OTIntervalSeconds * 1000; // Must multiply by 1000, because timers operate in milliseconds.
            tmrTradeOverTime.Start(); // Start the timer.
            stsOTProgress.Value = 0;
            stsOTProgress.Visible = true;
        }

        private void btnSellOverTimeOrder_Click(object sender, EventArgs e)
        {
            UpdateOverTimeSummary(); // Makes sure our variables are current.

            OTSide = "Sell";

            tmrTradeOverTime.Interval = OTIntervalSeconds * 1000; // Must multiply by 1000, because timers operate in milliseconds.
            tmrTradeOverTime.Start(); // Start the timer.
            stsOTProgress.Value = 0;
            stsOTProgress.Visible = true;
        }

        private void tmrTradeOverTime_Tick(object sender, EventArgs e)
        {
            OTTimerCount++;
            bitmex.MarketOrder(ActiveInstrument.Symbol, OTSide, OTContractsPer);

            double Percent = ((double)OTTimerCount / (double)OTIntervalCount) * 100;
            stsOTProgress.Value = Convert.ToInt32(Math.Round(Percent));

            if(OTTimerCount == OTIntervalCount)
            {
                OTTimerCount = 0;
                tmrTradeOverTime.Stop();
                stsOTProgress.Value = 0;
                stsOTProgress.Visible = false;
                
            }
        }

        private void btnOverTimeStop_Click(object sender, EventArgs e)
        {
            OTTimerCount = 0;
            stsOTProgress.Value = 0;
            stsOTProgress.Visible = false;
            tmrTradeOverTime.Stop();
        }
    }
}
