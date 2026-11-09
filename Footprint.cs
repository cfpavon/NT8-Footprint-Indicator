#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class Footprint : Indicator
	{
		//private Dictionary<double, int> deltaMap;   // price → delta
		
		// barIndex → (binPrice → bid/ask volumes)
        private Dictionary<int, Dictionary<double, (double bid, double ask, double vol)>> barBins;
		
		private Dictionary<int, Dictionary<double, (double bid, double ask, double vol)>> finalizedBins;
		//private Dictionary<int, Dictionary<double, (int bid, int ask)>> finalizedBins = new Dictionary<int, Dictionary<double, (int bid, int ask)>>();
		//private Dictionary<int, Dictionary<double, ValueTuple<double,double>>> barBins;
		//private Dictionary<int, Dictionary<double, ValueTuple<double,double>>> finalizedBins;

        //private Brush upBrushDX;
        //private Brush downBrushDX;
		private SharpDX.Direct2D1.Brush upBrushDX;
        private SharpDX.Direct2D1.Brush downBrushDX;
		private SharpDX.Direct2D1.Brush upBrushDX1;
        private SharpDX.Direct2D1.Brush downBrushDX1;
		private SharpDX.Direct2D1.Brush volBrushDX;
		private SharpDX.Direct2D1.Brush volBrushDX1;
		private SharpDX.Direct2D1.SolidColorBrush neutralBrushDX;
		private SharpDX.Direct2D1.SolidColorBrush neutralBrushDX1;
		
		private SharpDX.Direct2D1.SolidColorBrush valBrushDX;
		
		private SharpDX.DirectWrite.Factory factoryDW;
		private SharpDX.DirectWrite.TextFormat textFormat;
		
		private HashSet<int> closedBars = new HashSet<int>();
		private int CurrentBarN;
		
		private double lastTickPrice = 0.0;
		private int lastTickDirection = 0; // -1 = down, +1 = up, 0 = none yet
		
		private double lastTickPrice1 = 0.0;
		private int lastTickDirection1 = 0; // -1 = down, +1 = up, 0 = none yet
		
		private int primaryBar;
		
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "Footprint";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				CandleSpacing					= 15;
				ScaleFactor					= 0.001;
				BarHeight					= 5;
				BinSize = 1.5;
				RectWidth=30;
				RectHeight = 15;
				UseUptickClassification=false;
				
				
			}
			else if (State == State.Configure)
			{
				AddDataSeries("GC 12-26",Data.BarsPeriodType.Tick, 1);
				AddDataSeries(Data.BarsPeriodType.Tick, 1);
			}
			else if (State == State.DataLoaded)
            {
                //deltaMap = new Dictionary<double, int>();
				//barBins = new Dictionary<int, Dictionary<double, (double bid, double ask)>>();
				barBins = new Dictionary<int, Dictionary<double, (double bid, double ask, double vol)>>();
                finalizedBins = new Dictionary<int, Dictionary<double, (double bid, double ask, double vol)>>();
				
				ChartControl.Properties.BarDistance = 50;
				
				factoryDW = new SharpDX.DirectWrite.Factory();

		        textFormat = new SharpDX.DirectWrite.TextFormat(
		            factoryDW,
		            "Arial",
		            SharpDX.DirectWrite.FontWeight.Bold,      // bold
		            SharpDX.DirectWrite.FontStyle.Normal,
		            SharpDX.DirectWrite.FontStretch.Normal,
		            12                                     // font size
		        )
		        {
		            TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,   // right align
		            ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Near // vertical centering
		        };
            }
            else if (State == State.Historical)
            {
                // Brushes created in OnRender
            }
            else if (State == State.Terminated)
            {
                //upBrushDX?.Dispose();
                //downBrushDX?.Dispose();
            }
		}

		protected override void OnBarUpdate()
		{
//			int primaryBar=0; // anchor to primary bar index
			
//			// Tick series accumulation
//            if (BarsInProgress == 0)
//            {
//				// Only mark bars when they close
//			    if (IsFirstTickOfBar)
//					primaryBar = CurrentBars[0]; // anchor to primary bar index
//				else
//					return;
//			        //return;
			
////			    closedBars.Add(CurrentBar+1);
//			}
			//if (IsFirstTickOfBar) return;
			
			if ((BarsInProgress == 0))
		    {
				primaryBar= CurrentBar; 
			}
			
			// Tick series accumulation
            if (BarsInProgress == 1 )
            {
                //double price = Closes[1][0];
				//double vol = Volumes[1][0];
				
				double price = BarsArray[1].GetClose(CurrentBars[1]);
                
				//double bin = Math.Round(price / BinSize) * BinSize;
				double binFloor = Math.Floor(price / BinSize) * BinSize;
				double binMid   = binFloor + BinSize / 2.0;
				double vol = BarsArray[1].GetVolume(CurrentBars[1]);
				
				if (!barBins.ContainsKey(primaryBar))
                    barBins[primaryBar] = new Dictionary<double, (double bid, double ask, double vol)>();
					//barBins[CurrentBar] = new Dictionary<double, ValueTuple<double,double>>();

                if (!barBins[primaryBar].ContainsKey(binMid))
                    barBins[primaryBar][binMid] = (0, 0, 0);
				
				if(UseUptickClassification)
				{
					var entry = barBins[primaryBar][binMid]; 
					
					if (lastTickPrice == 0.0)
			        {
			            // First tick initialization
			            lastTickPrice = price;
			            lastTickDirection = 0;
			        }
				
				    if (price > lastTickPrice)
				    {
				        // Price moved up: Aggressive Market Buy hitting the Ask
				        entry.ask += vol;
						lastTickDirection = +1;
				    }
				    else if (price < lastTickPrice)
				    {
				        // Price moved down: Aggressive Market Sell hitting the Bid
				        entry.bid += vol;
						lastTickDirection = -1;
				    }
					else
			        {
			            // Flat tick → use last direction as tie-breaker
			            if (lastTickDirection == +1)
			                entry.ask += vol;
			            else if (lastTickDirection == -1)
			                entry.bid += vol;
			      
			        }
	//			    else
	//			    {
	//			        // Price stayed the same (Trade inside spread or same level): 
	//			        // Assign to the side that matches the last trade direction to maintain continuity
	//			        if (entry.ask >= entry.bid) entry.ask += vol;
	//			        else entry.bid += vol;
	//			    }
				
				    // Always increment the total volume container cleanly
				    entry.vol += vol;
				    
				    // Save state for the next incoming tick data point
				    barBins[primaryBar][binMid] = entry;
				    lastTickPrice = price; 
				}
				else
				{
					BidAskQuote(price,BarsArray[1],CurrentBars[1],binMid,1);
				}
				
				
            }
			
			
			if (BarsInProgress == 2 )
            {
                //double price = Closes[1][0];
				//double vol = Volumes[1][0];
				
				double price = BarsArray[2].GetClose(CurrentBars[2]);
                
				//double bin = Math.Round(price / BinSize) * BinSize;
				double binFloor = Math.Floor(price / BinSize) * BinSize;
				double binMid   = binFloor + BinSize / 2.0;
				
				
				
				
				double vol = BarsArray[2].GetVolume(CurrentBars[2]);

                 

                if (!barBins.ContainsKey(primaryBar))
                    barBins[primaryBar] = new Dictionary<double, (double bid, double ask, double vol)>();
					//barBins[CurrentBar] = new Dictionary<double, ValueTuple<double,double>>();

                if (!barBins[primaryBar].ContainsKey(binMid))
                    barBins[primaryBar][binMid] = (0, 0, 0);

                if(UseUptickClassification)
				{
					var entry = barBins[primaryBar][binMid];
					
					if (lastTickPrice1 == 0.0)
			        {
			            // First tick initialization
			            lastTickPrice1 = price;
			            lastTickDirection1 = 0;
			        }
				
				    if (price > lastTickPrice1)
				    {
				        // Price moved up: Aggressive Market Buy hitting the Ask
				        entry.ask += vol/10;
						lastTickDirection1 = +1;
				    }
				    else if (price < lastTickPrice1)
				    {
				        // Price moved down: Aggressive Market Sell hitting the Bid
				        entry.bid += vol/10;
						lastTickDirection1 = -1;
				    }
					else
			        {
			            // Flat tick → use last direction as tie-breaker
			            if (lastTickDirection1 == +1)
			                entry.ask += vol/10;
			            else if (lastTickDirection1 == -1)
			                entry.bid += vol/10;
			      
			        }
	//			    else
	//			    {
	//			        // Price stayed the same (Trade inside spread or same level): 
	//			        // Assign to the side that matches the last trade direction to maintain continuity
	//			        if (entry.ask >= entry.bid) entry.ask += vol;
	//			        else entry.bid += vol;
	//			    }
				
				    // Always increment the total volume container cleanly
				    entry.vol += vol/10;
				    
				    // Save state for the next incoming tick data point
				    barBins[primaryBar][binMid] = entry;
				    lastTickPrice1 = price; 
				}
				else
				{
					BidAskQuote(price,BarsArray[2],CurrentBars[2],binMid,10);
				}
            }
			
//			if (BarsInProgress==0)
//            {
//                finalizedBins[CurrentBar] = new Dictionary<double, (double bid, double ask)>(barBins[CurrentBar]);
//            }
			
			
			
//			if (BarsInProgress == 0)
//		    {
//		        int barIndex = CurrentBar;
//		        if (barBins.ContainsKey(barIndex))
//		        {
//		            // Deep copy current bins into finalizedBins
//		            //finalizedBins[barIndex] = new Dictionary<double, (double bid, double ask)>(barBins[barIndex]);
//					finalizedBins[CurrentBar] = new Dictionary<double, ValueTuple<double,double>>(barBins[CurrentBar]);
//		        }
//		    }
			
			// Tick series accumulation
//            if (BarsInProgress == 0)
//            {
//				// Only mark bars when they close
////			    if (IsFirstTickOfBar)
////			        return;
			
////			    closedBars.Add(CurrentBar+1);
//			}
			
			
			
		}
		
		private void TickTestClassification(double price, double volume, double binMid, double lastPrice,int lastTickDir,double Multiplier)
		{
			
			var entry = barBins[primaryBar][binMid];
				
			if (price > lastPrice)
		    {
		        // Price moved up: Aggressive Market Buy hitting the Ask
		        entry.ask += volume/Multiplier;
				lastTickDir = +1;
		    }
		    else if (price < lastPrice)
		    {
		        // Price moved down: Aggressive Market Sell hitting the Bid
		        entry.bid += volume/Multiplier;
				lastTickDir = -1;
		    }
			else
	        {
	            // Flat tick → use last direction as tie-breaker
	            if (lastTickDir == +1)
	                entry.ask += volume/Multiplier;
	            else if (lastTickDir == -1)
	                entry.bid += volume/Multiplier;
	      
	        }
			entry.vol+=volume;
			
			barBins[primaryBar][binMid] = entry;
			
			 
			
		}
		
		private void BidAskQuote(double price,Bars bars, int currentBar,double binMid,double Multiplier)
		{
			double ask = bars.GetAsk(currentBar);
			double bid = bars.GetBid(currentBar);
			double vol = bars.GetVolume(currentBar);
				
				

            bool isAsk = (price >= ask);
            bool isBid = (price <= bid);

            //int primaryBar = CurrentBars[0]; // anchor to primary bar index

           

            var entry = barBins[primaryBar][binMid];
            if (isAsk) entry.ask += vol/Multiplier;
            else if (isBid) entry.bid += vol/Multiplier;
			entry.vol+=vol;
			
			
            barBins[primaryBar][binMid] = entry;
		}
		
		
//		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
//        {
			
//			if (upBrushDX == null)
//			    upBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(50, 205, 50, 186)); // green
			
//			if (downBrushDX == null)
//			    downBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(138, 43, 226, 186)); // red
			
//			if (neutralBrushDX == null)
//    			neutralBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(128, 255, 255, 255));
			
//			if (neutralBrushDX1 == null)
//    			neutralBrushDX1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(64,64,64,255));
			
//            foreach (var kvp in barBins)
//            {
//                int barIndex = kvp.Key;
//                var bins = kvp.Value;

//                if (bins.Count == 0) continue;

//                var keys = bins.Keys.ToList();
//                keys.Sort();

//                double minMid = keys.First();
//                double maxMid = keys.Last();
//                double minPrice = minMid - BinSize / 2.0;
//                double maxPrice = maxMid + BinSize / 2.0;

//                float yMin = chartScale.GetYByValue(minPrice);
//                float yMax = chartScale.GetYByValue(maxPrice);
//                float span = yMin - yMax;
//                float rectHeight = span / keys.Count;

//                float candleX = chartControl.GetXByBarIndex(ChartBars, barIndex);
//                float xBase = candleX + (float)chartControl.BarWidth + CandleSpacing;

//                int i = 0;
//                foreach (var midPrice in keys)
//                {
//                    double delta = bins[midPrice].Item1 - bins[midPrice].Item2;
//                    float y = yMax + i * rectHeight;
					
	
//					 //        Fixed rectangle
//			        var rect = new SharpDX.RectangleF(xBase, y, RectWidth, rectHeight);
			
//			        // Fill color by delta
//			        var brush = delta > 0 ? upBrushDX : (delta < 0 ? downBrushDX : neutralBrushDX);
					
//					var brushTxt = delta > 0 ? neutralBrushDX1 : neutralBrushDX;
//			        RenderTarget.FillRectangle(rect, brush);
			
//			        // Draw text (delta value)
//			        var layout = new SharpDX.DirectWrite.TextLayout(
//			            factoryDW,
//			            delta.ToString(),
//			            textFormat,
//			            RectWidth,
//			            rectHeight);
			
//			        RenderTarget.DrawTextLayout(
//			            new SharpDX.Vector2(xBase, y),
//			            layout,
//			            brushTxt);
					
					
/////
////                    var rect = new SharpDX.RectangleF(xBase, y, RectWidth, rectHeight);
////                    var brush = delta > 0 ? upBrushDX : (delta < 0 ? downBrushDX : neutralBrushDX);

////                    RenderTarget.FillRectangle(rect, brush);

////                    var layout = new SharpDX.DirectWrite.TextLayout(factoryDW, delta.ToString(), textFormat, RectWidth, rectHeight);
////                    RenderTarget.DrawTextLayout(new Vector2(xBase, y), layout, neutralBrushDX1);
//                    //layout.Dispose();

//                    i++;
//                }
//            }
//        }
		
//		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
//        {
			
//			if (barBins == null || barBins.Count == 0) return;
			
			

//			if (upBrushDX == null)
//			    upBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(50, 205, 50, 186)); // green
			
//			if (downBrushDX == null)
//			    downBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(138, 43, 226, 186)); // red
			
//			if (neutralBrushDX == null)
//    			neutralBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(128, 255, 255, 255));
			
//			if (neutralBrushDX1 == null)
//    			neutralBrushDX1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(64,64,64,255));
			
////			var textFormat = new SharpDX.DirectWrite.TextFormat(
////			    new SharpDX.DirectWrite.Factory(),
////			    "Arial", 
////				SharpDX.DirectWrite.FontWeight.Bold,   // <--- bold
////    			SharpDX.DirectWrite.FontStyle.Normal,  // italic if you want
////    			SharpDX.DirectWrite.FontStretch.Normal,
////				14f);
			
//			foreach (var kvp in barBins)
//			{
//			    int barIndex = kvp.Key;
				
//				// Only render if this bar has closed
//		        //if (!closedBars.Contains(barIndex))
//		        //    continue;
//			    //float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex) + CandleSpacing;
//				//float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex) + (float)chartControl.BarWidth+ CandleSpacing;
//				float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex)+ (float)chartControl.BarWidth+ CandleSpacing;
				
//				var keys = kvp.Value.Keys.ToList();
//				keys.Sort();
//				double minPrice = keys.First() - BinSize / 2.0;
//				double maxPrice = keys.Last() + BinSize / 2.0;
			
//			    int i = 0;
//			    foreach (var bin in kvp.)
//			    {
//			        double price = bin.Key;
//			        double bidVol = bin.Value.Item2;
//			        double askVol = bin.Value.Item1;
//			        double delta = askVol - bidVol;
					
//					float yMin = chartScale.GetYByValue(minPrice);
//					float yMax = chartScale.GetYByValue(maxPrice);
//					float span = yMin - yMax; // vertical pixel span
					
//					int binCount = keys.Count;
//					int rectHeight = (int) span / binCount;
					
					
					
					
			
//			        float yCenter = chartScale.GetYByValue(price);
//					float yTop    = yCenter - RectHeight / 2f;
			
//			        // Fixed rectangle
//			        var rect = new SharpDX.RectangleF(xBase, yTop, RectWidth, rectHeight);
			
//			        // Fill color by delta
//			        var brush = delta > 0 ? upBrushDX : (delta < 0 ? downBrushDX : neutralBrushDX);
					
//					var brushTxt = delta > 0 ? neutralBrushDX1 : neutralBrushDX;
//			        RenderTarget.FillRectangle(rect, brush);
			
//			        // Draw text (delta value)
//			        var layout = new SharpDX.DirectWrite.TextLayout(
//			            factoryDW,
//			            delta.ToString(),
//			            textFormat,
//			            RectWidth,
//			            rectHeight);
			
//			        RenderTarget.DrawTextLayout(
//			            new SharpDX.Vector2(xBase, yTop),
//			            layout,
//			            brushTxt);
			        
//			        i++;
//			    }
//			}
//		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (barBins == null || barBins.Count == 0) return;

			

			

            foreach (var kvp in barBins)
            {
                int barIndex = kvp.Key;
				
				double candleWidth = chartControl.BarWidth;
				float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex) - (float)candleWidth / 2f+chartControl.Properties.BarDistance-10f;
				//float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex) - (float)candleWidth / 2f-10f;
				float xVol = xBase-25f;
				float xDelta = xBase+((float)chartControl.BarWidth)+15f;
                //float xBase = chartControl.GetXByBarIndex(ChartBars, barIndex) + CandleSpacing;
				
				float xBase1= xBase+((float)chartControl.BarWidth)+20f;
				
				float xRight= xBase+7f;
				float xLeft= xBase-7f;
				
				
				
				var keys = kvp.Value.Keys.ToList();
				keys.Sort();
				double minPrice = keys.First() - BinSize / 2.0;
				double maxPrice = keys.Last() + BinSize / 2.0;
				double totalDelta = 0;
				double totalAsk = 0;
				double totalBid = 0;
				double totalActiveVol=0;
				//double HHI=0;
				//double HHIdelta
				
				
				
				var levels = kvp.Value.Select(kvp1 => new { Price = kvp1.Key, Vol = kvp1.Value.vol,AcVol = kvp1.Value.ask-kvp1.Value.bid })
                 .OrderBy(x => x.Price)
                 .ToList();
				double totalVol = levels.Sum(l => l.Vol);
				
				//double totalActiveVol =  levels.Sum(l => l.AcVol);
				double mean = levels.Average(x => x.AcVol);
				double variance = levels.Average(x => Math.Pow(x.AcVol - mean, 2));
				double stdDev = Math.Sqrt(variance);
				
				double skewness = levels.Average(x => Math.Pow(x.AcVol - mean, 3)) / Math.Pow(stdDev, 3);
				
				double coefVar=stdDev/mean*100;
				
				
				double cumulative = 0;
				var cumLevels = new List<(double Price, double CumPct)>();
				
				foreach (var l in levels)
				{
				    cumulative += l.Vol;
				    double pct = cumulative / totalVol;
				    cumLevels.Add((l.Price, pct));
				}
				
				double VAH = cumLevels.First(l => l.CumPct >= 0.85).Price;
				double VAL = cumLevels.First(l => l.CumPct >= 0.15).Price;

                foreach (var bin in kvp.Value)
                {
                    double price = bin.Key;
                    double bidVol = bin.Value.bid;
                    double askVol = bin.Value.ask;
                    double delta = askVol - bidVol;
					double vol =askVol + bidVol;;
					
					totalDelta+=delta;
					totalAsk+=askVol;
					totalBid+=bidVol;
					totalActiveVol+=vol;
					//HHI+=Math.Pow(((askVol+bidVol)/totalActiveVol)*100,2);
					
					float yMin = chartScale.GetYByValue(minPrice);
					float yMax = chartScale.GetYByValue(maxPrice);
					float span = yMin - yMax; // vertical pixel span
					
					int binCount = keys.Count;
					int rectHeight = (int) span / binCount;
				

                    if (bidVol == 0 && askVol == 0) continue;

                    float y = chartScale.GetYByValue(price);
					
					//float candleWidth = chartControl.BarWidth;   // actual candle width in pixels
					float spacing     = chartControl.Properties.BarDistance; // pixel spacing between bars
					
					
					
                    //float width = -Math.Max((float)(vol * ScaleFactor*spacing),spacing);
					float width = -(float)(vol * ScaleFactor*spacing);

                    RectangleF rect = new RectangleF(xBase, y, width, rectHeight);
                    RenderTarget.FillRectangle(rect, volBrushDX);
					// Draw border
					RenderTarget.DrawRectangle(rect, volBrushDX1, 1.0f);
					
					
					
                    float width1 = (float)(Math.Abs(delta) * ScaleFactor*spacing*3.5);
					
					
					RectangleF rect1 = new RectangleF(xBase1, y, width1, rectHeight);
                    RenderTarget.FillRectangle(rect1, delta > 0 ? upBrushDX : (delta < 0 ? downBrushDX : volBrushDX));
					// Draw border
					RenderTarget.DrawRectangle(rect1, delta > 0 ? upBrushDX1 : (delta < 0 ? downBrushDX1 : volBrushDX), 1.0f);
					
					
                }
				
				//Print("Values:  "+kvp.Key.ToString()+"  HHI: "+HHI.ToString());
				// Convert price levels to Y pixel coordinates
				float yVAH = chartScale.GetYByValue(VAH);
				float yVAL = chartScale.GetYByValue(VAL);
				
				// Draw VAH line
				RenderTarget.DrawLine(new Vector2(xLeft, yVAH), new Vector2(xRight, yVAH), valBrushDX, 2.0f);
				
				// Draw VAL line
				RenderTarget.DrawLine(new Vector2(xLeft, yVAL), new Vector2(xRight, yVAL), valBrushDX, 2.0f);
				
				//double normalizedDelta=((totalAsk-totalBid)/(totalAsk+totalBid))*100;
				double normalizedDelta=((totalAsk-totalBid));
				
				float yPrice1 = chartScale.GetYByValue(minPrice- 2*(BinSize / 2.0));
				// Draw text (delta value)
		        var layout4 = new SharpDX.DirectWrite.TextLayout(
		            factoryDW,
		            FormatVolume(normalizedDelta),
		            textFormat,
		            RectWidth,
		            RectHeight);
		
		        RenderTarget.DrawTextLayout(
		            new SharpDX.Vector2(xDelta, yPrice1),
		            layout4,
		            totalDelta > 0 ? upBrushDX1 : (totalDelta < 0 ? downBrushDX1 : volBrushDX));
				
				var layout5 = new SharpDX.DirectWrite.TextLayout(
		            factoryDW,
		            FormatVolume(totalActiveVol),
		            textFormat,
		            RectWidth,
		            RectHeight);
		
		        RenderTarget.DrawTextLayout(
		            new SharpDX.Vector2(xVol, yPrice1),
		            layout5,
		            valBrushDX);
				
//				float yPrice2 = chartScale.GetYByValue(minPrice- 4*(BinSize / 2.0));
//				// Draw text (delta value)
//		        var layout6 = new SharpDX.DirectWrite.TextLayout(
//		            factoryDW,
//		            stdDev.ToString("F1"),
//		            textFormat,
//		            RectWidth,
//		            RectHeight);
		
//		        RenderTarget.DrawTextLayout(
//		            new SharpDX.Vector2(xDelta, yPrice2),
//		            layout6,
//		            valBrushDX);
				
				
				
				
            }
        }
		
		private void DisposeBrushes()
		{
			if (upBrushDX != null) { upBrushDX.Dispose(); upBrushDX = null; }
		    if (downBrushDX != null) { downBrushDX.Dispose(); downBrushDX = null; }
			if (upBrushDX1 != null) { upBrushDX1.Dispose(); upBrushDX1 = null; }
		    if (downBrushDX1 != null) { downBrushDX1.Dispose(); downBrushDX1 = null; }
			if (volBrushDX != null) { volBrushDX.Dispose(); volBrushDX = null; }
			if (volBrushDX1 != null) { volBrushDX1.Dispose(); volBrushDX1 = null; }
			if (valBrushDX != null) { valBrushDX.Dispose(); valBrushDX = null; }
		    
		    
		}
		
		public override void OnRenderTargetChanged()
		{
			DisposeBrushes();
			if (RenderTarget != null)
    		{
				if (upBrushDX == null)
			    	upBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 255, 0, 96)); // green
			
				if (downBrushDX == null)
				    downBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 0, 0, 96)); // red
				
				if (upBrushDX1 == null)
				    upBrushDX1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 255, 0, 256)); // green
				
				if (downBrushDX1 == null)
				    downBrushDX1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 0, 0, 256)); // red
	
				if (volBrushDX == null)
				    volBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(128, 128, 128, 96)); // red
				
				if (volBrushDX1 == null)
				    volBrushDX1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(128, 128, 128, 256)); // red
				
				if (valBrushDX == null)
				    valBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 128, 256)); // red
					
			}
		}
		
		private string FormatVolume(double vol)
		{
		    if (vol >= 1_000_000)
		        return (vol / 1_000_000d).ToString("0.#") + "M"; // millions
		    else if (vol >= 1_000)
		        return (vol / 1_000d).ToString("0.#") + "k";     // thousands
		    else
		        return vol.ToString("F0");                        // plain number
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(-200, int.MaxValue)]
		[Display(Name="CandleSpacing", Order=1, GroupName="Parameters")]
		public int CandleSpacing
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name="ScaleFactor", Order=2, GroupName="Parameters")]
		public double ScaleFactor
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="BarHeight", Order=3, GroupName="Parameters")]
		public int BarHeight
		{ get; set; }
		
		[Range(0.25, 5)]
        [Display(Name = "Bin Size (points)", Order = 4, GroupName = "Parameters")]
        public double BinSize { get; set; }
		
		[Range(10, 100)]
		[Display(Name = "Rect Width", Order = 5, GroupName = "Parameters")]
		public int RectWidth { get; set; }
		
		[Range(10, 50)]
		[Display(Name = "Rect Height", Order = 6, GroupName = "Parameters")]
		public int RectHeight { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="UseUptickClassification", Order=1, GroupName="Method")]
		public bool UseUptickClassification { get; set; }
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Footprint[] cacheFootprint;
		public Footprint Footprint(int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			return Footprint(Input, candleSpacing, scaleFactor, barHeight, useUptickClassification);
		}

		public Footprint Footprint(ISeries<double> input, int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			if (cacheFootprint != null)
				for (int idx = 0; idx < cacheFootprint.Length; idx++)
					if (cacheFootprint[idx] != null && cacheFootprint[idx].CandleSpacing == candleSpacing && cacheFootprint[idx].ScaleFactor == scaleFactor && cacheFootprint[idx].BarHeight == barHeight && cacheFootprint[idx].UseUptickClassification == useUptickClassification && cacheFootprint[idx].EqualsInput(input))
						return cacheFootprint[idx];
			return CacheIndicator<Footprint>(new Footprint(){ CandleSpacing = candleSpacing, ScaleFactor = scaleFactor, BarHeight = barHeight, UseUptickClassification = useUptickClassification }, input, ref cacheFootprint);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Footprint Footprint(int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			return indicator.Footprint(Input, candleSpacing, scaleFactor, barHeight, useUptickClassification);
		}

		public Indicators.Footprint Footprint(ISeries<double> input , int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			return indicator.Footprint(input, candleSpacing, scaleFactor, barHeight, useUptickClassification);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Footprint Footprint(int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			return indicator.Footprint(Input, candleSpacing, scaleFactor, barHeight, useUptickClassification);
		}

		public Indicators.Footprint Footprint(ISeries<double> input , int candleSpacing, double scaleFactor, int barHeight, bool useUptickClassification)
		{
			return indicator.Footprint(input, candleSpacing, scaleFactor, barHeight, useUptickClassification);
		}
	}
}

#endregion
