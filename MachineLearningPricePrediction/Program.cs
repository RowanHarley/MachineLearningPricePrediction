using Accord.MachineLearning.DecisionTrees;
using Accord.MachineLearning.DecisionTrees.Learning;
using Accord.IO;
using Accord.Statistics.Analysis;

namespace MachineLearningPricePrediction
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            List<(double, double)> Close = new();
            List<double> MidClose = new();
            List<(double, double)> High = new();
            List<(double, double)> Low = new();
            using (var reader = new StreamReader(@"C:\Users\rowan\OneDrive\Desktop\Data Testing\Option Hedging\QuantConnect Data\NQ\2021-06-04 NQ OHLC.csv"))
            {
                bool ReadHeader = false;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!ReadHeader)
                    {
                        ReadHeader = true;
                        continue;
                    }
                    var values = line.Split(',');

                    Close.Add((double.Parse(values[2]), double.Parse(values[6])));
                    High.Add((double.Parse(values[3]), double.Parse(values[7])));
                    Low.Add((double.Parse(values[4]), double.Parse(values[8])));
                }
            }
            using (var reader = new StreamReader(@"C:\Users\rowan\OneDrive\Desktop\Data Testing\Option Hedging\QuantConnect Data\NQ\2021-09-02 NQ OHLC.csv"))
            {
                bool ReadHeader = false;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!ReadHeader)
                    {
                        ReadHeader = true;
                        continue;
                    }
                    var values = line.Split(',');

                    Close.Add((double.Parse(values[2]), double.Parse(values[6])));
                    High.Add((double.Parse(values[3]), double.Parse(values[7])));
                    Low.Add((double.Parse(values[4]), double.Parse(values[8])));
                    MidClose.Add((double.Parse(values[2]) + double.Parse(values[6])) / 2);
                }
            }
            var minMove = 19;
            var commission = 4.4;
            // Generate training and testing data

            var data = DataManagement.GenerateData(Close, High, Low, MidClose);

            // Define the feature columns
            var featureColumns = Enumerable.Range(0, 200).ToArray();


            // Define the decision tree learning algorithm
            var algorithm = new C45Learning()
            {
                Attributes = featureColumns.Select(i => new DecisionVariable($"Price {i + 1}", DecisionVariableKind.Continuous)).ToArray()
            };
            // Train the decision tree classifier
            var TrainingAmount = 1000; // (int)(data.Count * 0.5);
            var TrainingSet = data.Take(TrainingAmount).ToArray();
            var TestSet = data.Skip(TrainingAmount).Take(data.Count - TrainingAmount).ToArray();
            List<double[]> TrainingPrices = new();
            List<int> TrainingOutputs = new();
            List<double[]> TestPrices = new();
            List<int> TestOutputs = new();
            for (int i = 0; i < TrainingSet.Length; i++)
            {
                TrainingPrices.Add(TrainingSet[i].Prices.Take(200).ToArray());
                TrainingOutputs.Add((int)TrainingSet[i].Action);
                if (i < TestSet.Length)
                {
                    TestPrices.Add(TestSet[i].Prices.Take(200).ToArray());
                    TestOutputs.Add((int)TestSet[i].Action);
                }
            }
            double[][] x = TrainingPrices.ToArray();
            int[] y = TrainingOutputs.ToArray();
            Console.WriteLine("Learning...");
            var tree = algorithm.Learn(x, y);
            Console.WriteLine("Finished training tree!");
            // Evaluate the classifier on the testing data
            var preds = TestSet.Select(d => tree.Decide(d.Prices.Take(200).ToArray())).ToArray();
            var act = TestSet.Select(d => (int)d.Action).ToArray();
            var PnL = new List<double>();
            for (int i = 0; i < TestSet.Length; i++)
            {
                for (int j = 0; j < TestSet[i].Prices.Length; j++)
                {
                    TestSet[i].Prices[j] *= TestSet[i].Multiplier;
                    TestSet[i].Prices[j] = Math.Round(TestSet[i].Prices[j], 3);
                }
            }
            for (int i = 0; i < TestSet.Length; i++)
            {
                if (preds[i] == (int)DataManagement.TradingAction.DoNothing)
                    PnL.Add(0);
                else if (preds[i] == (int)DataManagement.TradingAction.Buy)
                {
                    double[] UnknownPrices = TestSet[i].Prices.Skip(200).Take(20).ToArray();
                    var LastPrc = TestSet[i].Prices[199];
                    bool CanSell = false;
                    bool ActionSet = false;
                    /*foreach (var prc in UnknownPrices)
                    {
                        if (prc < TestSet[i].Prices[199] - 5)
                        {
                            PnL.Add(-5 * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                        else if (prc > TestSet[i].Prices[199] + minMove && !CanSell)
                        {
                            LastPrc = prc;
                            CanSell = true;
                        }
                        else if (CanSell && prc < LastPrc)
                        {
                            PnL.Add((LastPrc - 0.25 - TestSet[i].Prices[199]) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                    }*/
                    for (int j = 0; j < UnknownPrices.Length; j++)
                    {
                        if (TestSet[i].Lows[199 + j].Item1 < TestSet[i].Closes[199].Item2 - 5)
                        {
                            // Worst Case Scenario Below
                            PnL.Add((TestSet[i].Lows[199 + j].Item1 - TestSet[i].Closes[199].Item2) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                        else if (UnknownPrices[j] > TestSet[i].Closes[199].Item2 + minMove && !CanSell)
                        {
                            LastPrc = UnknownPrices[j];
                            CanSell = true;
                        }
                        else if (CanSell && TestSet[i].Lows[199 + j].Item1 < LastPrc - 1)
                        {
                            PnL.Add((TestSet[i].Lows[199 + j].Item1 - TestSet[i].Closes[199].Item2) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                    }

                    if (!ActionSet)
                        PnL.Add((TestSet[i].Closes[^1].Item1 - 0.25 - TestSet[i].Closes[199].Item2) * 20 - commission);
                }
                else
                {
                    double[] UnknownPrices = TestSet[i].Prices.Skip(200).Take(20).ToArray();
                    var LastPrc = TestSet[i].Prices[199];
                    bool CanSell = false;
                    bool ActionSet = false;
                    foreach (var prc in UnknownPrices)
                    {
                        if (prc > TestSet[i].Prices[199] + 5)
                        {
                            PnL.Add(-5 * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                        else if (prc < TestSet[i].Prices[199] - minMove && !CanSell)
                        {
                            LastPrc = prc;
                            CanSell = true;
                        }
                        else if (CanSell && prc > LastPrc)
                        {
                            PnL.Add(( - 0.25 + TestSet[i].Prices[199] - LastPrc) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                    }
                    for (int j = 0; j < UnknownPrices.Length; j++)
                    {
                        if (TestSet[i].Highs[199 + j].Item2 > TestSet[i].Closes[199].Item1 + 5)
                        {
                            // Worst Case Scenario Below
                            PnL.Add((TestSet[i].Highs[199 + j].Item2 - TestSet[i].Closes[199].Item1) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                        else if (UnknownPrices[j] < TestSet[i].Closes[199].Item1 - minMove && !CanSell)
                        {
                            LastPrc = UnknownPrices[j];
                            CanSell = true;
                        }
                        else if (CanSell && TestSet[i].Highs[199 + j].Item2 > LastPrc + 1)
                        {
                            PnL.Add((TestSet[i].Highs[199 + j].Item2 - TestSet[i].Closes[199].Item1) * 20 - commission);
                            ActionSet = true;
                            break;
                        }
                    }
                    if (!ActionSet)
                        PnL.Add(( - 0.25 + TestSet[i].Closes[199].Item1 - TestSet[i].Closes[^1].Item2) * 20 - commission);
                }
            }
            var confusionMatrix = new GeneralConfusionMatrix(act, preds);
            Console.WriteLine($"Accuracy: {confusionMatrix.Accuracy}");
            Console.WriteLine($"Expected Value:");
            var matrix = confusionMatrix.ExpectedValues;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Console.Write(matrix[i, j] + "\t");
                }
                Console.WriteLine();
            }
            Console.WriteLine("Confusion Matrix: ");
            var confM = confusionMatrix.Matrix;
            for (int i = 0; i < confM.GetLength(0); i++)
            {
                for (int j = 0; j < confM.GetLength(1); j++)
                {
                    Console.Write(confM[i, j] + "\t");
                }
                Console.WriteLine();
            }
            Console.WriteLine("Proportions:");
            matrix = confusionMatrix.ProportionMatrix;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Console.Write(Math.Round(matrix[i, j] * 100,2) + "\t");
                }
                Console.WriteLine();
            }
            Console.WriteLine($"Matthews Correlation Coefficient: {confusionMatrix.Phi}");
            Console.WriteLine($"Total PnL: ${PnL.Sum()}");
            Console.WriteLine($"Total Positions Created: {PnL.Where(x => x != 0).Count()}");
            Serializer.Save(tree, @"C:\Users\rowan\OneDrive\Desktop\Data Testing\Option Hedging\QuantConnect Data\NQ\DecisionTree.tree");
            // Console.WriteLine($"Precision (Sell): {confusionMatrix.}");
            // Console.WriteLine($"Precision (Do nothing): {confusionMatrix.Precision(2)}");
        }

    }
}