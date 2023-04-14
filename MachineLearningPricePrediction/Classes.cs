namespace MachineLearningPricePrediction
{
    // Define a class to hold the closing price data
    public class StockData
    {
        public double[] Prices { get; set; }
        public (double,double)[] Highs { get; set; }
        public (double, double)[] Lows { get; set; }
        public (double, double)[] Closes { get; set; }

        public DataManagement.TradingAction Action { get; set; }
        public double Multiplier { get; set; }
    }

    // Define the criteria for buying, selling, or doing nothing
    public class DataManagement
    {
        public enum TradingAction
        {
            Buy,
            Sell,
            DoNothing
        }
        // Define the method to convert a TradingAction enum to a string
        public static string ActionToString(TradingAction action)
        {
            switch (action)
            {
                case TradingAction.Buy:
                    return "Buy";
                case TradingAction.Sell:
                    return "Sell";
                case TradingAction.DoNothing:
                    return "Do nothing";
                default:
                    throw new ArgumentException("Invalid action: " + action);
            }
        }

        // Define the method to convert a string to a TradingAction enum
        public static TradingAction StringToAction(string actionString)
        {
            switch (actionString)
            {
                case "Buy":
                    return TradingAction.Buy;
                case "Sell":
                    return TradingAction.Sell;
                case "Do nothing":
                    return TradingAction.DoNothing;
                default:
                    throw new ArgumentException("Invalid action string: " + actionString);
            }
        }

        // Define the method to generate the training and testing data
        public static List<StockData> GenerateData(List<(double, double)> Close, List<(double, double)> High, List<(double, double)> Low, List<double> MidClose)
        {
            List<List<(double, double)>[]> Datasets = new();
            List<List<double>> Prices = new();
            for (int i = 0; i < MidClose.Count; i+= 20)
            {
                if (i + 220 >= MidClose.Count)
                    break;
                Datasets.Add(new[] { Close.Take(new Range(new Index(i), new Index(i + 220))).ToList(), 
                    High.Take(new Range(new Index(i), new Index(i + 220))).ToList(), 
                    Low.Take(new Range(new Index(i), new Index(i + 220))).ToList() });
                Prices.Add(MidClose.Take(new Range(new Index(i), new Index(i + 220))).ToList());
            }

            int examplesPerAction = Datasets.Count;

            // Define the range of prices and the number of prices in each example
            
            Random rng = new();
            Datasets = Datasets.OrderBy(a => rng.Next()).ToList();
            List<StockData> data = new();

            for (int i = 0; i < examplesPerAction; i++)
            {
                double minMove = 19;
                var initPrice = Prices[i][0];
                double[] prices = Prices[i].Select(x => x/initPrice).ToArray();
                if (prices.Length < 220)
                    break;

                TradingAction action = TradingAction.DoNothing;

                // Determine the minimum and maximum prices over the next 20 days
                double minPrice = prices.Skip(200).Take(20).Min();
                double maxPrice = prices.Skip(200).Take(20).Max();
                double[] UnknownPrices = prices.Skip(200).Take(20).ToArray();
                minMove /= initPrice;

                // If buying is the correct action, check that the maximum price occurs before the minimum price
                if (maxPrice - prices[199] < minMove && prices[199] - minPrice < minMove)
                {
                    action = TradingAction.DoNothing;
                }
                else if (maxPrice - prices[199] >= minMove)
                {
                    var LastPrc = prices[199];
                    bool CanSell = false;
                    bool ActionSet = false;
                    /*foreach (var prc in UnknownPrices)
                    {
                        if (prc < prices[199] - 5)
                        {
                            action = TradingAction.DoNothing;
                            ActionSet = true;
                            break;
                        }
                        else if (prc > prices[199] + minMove && !CanSell)
                        {
                            LastPrc = prc;
                            CanSell = true;
                        }
                        else if(CanSell && prc < LastPrc)
                        {
                            action = TradingAction.Buy;
                            ActionSet = true;
                            break;
                        }
                    }*/
                    for (int j = 0; j < UnknownPrices.Length; j++)
                    {
                        if (Datasets[i][2][199 + j].Item1 < prices[199] - 5)
                        {
                            action = TradingAction.DoNothing;
                            ActionSet = true;
                            break;
                        }
                        else if (UnknownPrices[j] > prices[199] + minMove && !CanSell)
                        {
                            LastPrc = UnknownPrices[j];
                            CanSell = true;
                        }
                        else if (CanSell && Datasets[i][2][199 + j].Item1 < LastPrc - 1)
                        {
                            action = TradingAction.Buy;
                            ActionSet = true;
                            break;
                        }
                    }


                    if(!ActionSet)
                        action = TradingAction.Buy;

                }
                else if(prices[199] - minPrice >= minMove)
                {
                    var LastPrc = prices[199];
                    bool CanBuy = false;
                    bool ActionSet = false;
                    /*foreach (var prc in UnknownPrices)
                    {
                        if (prc > prices[199] + 5)
                        {
                            action = TradingAction.DoNothing;
                            ActionSet = true;
                            break;
                        }
                        else if (prc < prices[199] - minMove && !CanBuy)
                        {
                            LastPrc = prc;
                            CanBuy = true;
                        }
                        else if (CanBuy && prc > LastPrc)
                        {
                            action = TradingAction.Sell;
                            ActionSet = true;
                            break;
                        }
                    }*/
                    for (int j = 0; j < UnknownPrices.Length; j++)
                    {
                        if (Datasets[i][1][199 + j].Item1 > prices[199] + 5)
                        {
                            action = TradingAction.DoNothing;
                            ActionSet = true;
                            break;
                        }
                        else if (UnknownPrices[j] < prices[199] - minMove && !CanBuy)
                        {
                            LastPrc = UnknownPrices[j];
                            CanBuy = true;
                        }
                        else if (CanBuy && Datasets[i][1][199 + j].Item1 > LastPrc + 1)
                        {
                            action = TradingAction.Sell;
                            ActionSet = true;
                            break;
                        }
                    }
                    if (!ActionSet)
                        action = TradingAction.Sell;
                }
                // Add the example to the data
                data.Add(new StockData
                {
                    Prices = prices,
                    Highs = Datasets[i][1].ToArray(),
                    Lows = Datasets[i][2].ToArray(),
                    Closes = Datasets[i][0].ToArray(),
                    Action = action,
                    Multiplier = initPrice
                });
            }

            return data;
        }
    }
}
