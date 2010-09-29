// Copyright (C) 2010 Steffen Rendle, Zeno Gantner, Christoph Freudenthaler
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MyMediaLite.data_type;
using MyMediaLite.util;


namespace MyMediaLite.item_recommender
{
    /// <summary>
    /// Abstract class for matrix factorization based item predictors
    /// </summary>
    /// <author>Steffen Rendle, Christoph Freudenthaler, Zeno Gantner, University of Hildesheim</author>
    public abstract class MF : Memory, IterativeModel
    {
		// TODO call iterations 'epochs'

        /// <summary>User feature matrix</summary>
        protected Matrix<double> user_feature;
        /// <summary>Item feature matrix</summary>
        protected Matrix<double> item_feature;

        /// <summary>Number of features</summary>
        public int num_features = 10;
        /// <summary>Mean of the normal distribution used to initialize the features</summary>
        public double init_f_mean = 0;
        /// <summary>Standard deviation of the normal distribution used to initialize the features</summary>
        public double init_f_stdev = 0.1;
        /// <summary>Number of iterations over the training data</summary>
		public int NumIter { get { return num_iter; } set { num_iter = value; } }
        protected int num_iter = 30;

        /// <inheritdoc />
        public override void Train()
        {
			user_feature = new Matrix<double>(max_user_id + 1, num_features);
        	item_feature = new Matrix<double>(max_item_id + 1, num_features);

            MatrixUtils.InitNormal(user_feature, init_f_mean, init_f_stdev);
        	MatrixUtils.InitNormal(item_feature, init_f_mean, init_f_stdev);

			for (int i = 0; i < num_iter; i++)
			{
				Iterate();
				System.Console.Error.WriteLine(i);
			}
        }

		/// <summary>
		/// Iterate once over the data
		/// </summary>
		/// <returns>true if training should be aborted</returns>
		public abstract void Iterate();

		/// <summary>
		/// Computes the fit (optimization criterion) on the training data
		/// </summary>
		/// <returns>
		/// A <see cref="System.Double"/> representing the fit
		/// </returns>
		public abstract double ComputeFit();

		/// <summary>
		/// Predict the weight for a given user-item combination.
		///
		/// If the user or the item are not known to the engine, zero is returned.
		/// To avoid this behavior for unknown entities, use CanPredictRating() to check before.
		/// </summary>
		/// <param name="user_id">the user ID</param>
		/// <param name="item_id">the item ID</param>
		/// <returns>the predicted weight</returns>
        public override double Predict(int user_id, int item_id)
        {
            if ((user_id < 0) || (user_id >= user_feature.dim1))
            {
                Console.Error.WriteLine("user is unknown: " + user_id);
				return 0;
            }
            if ((item_id < 0) || (item_id >= item_feature.dim1))
            {
                Console.Error.WriteLine("item is unknown: " + item_id);
				return 0;
            }

            double result = 0;
            for (int f = 0; f < num_features; f++)
            {
                result += user_feature.Get(user_id, f) * item_feature.Get(item_id, f);
            }
            return result;
        }

		/// <inheritdoc />
		public override void SaveModel(string fileName)
		{
			// TODO replace by System.Globalization.CultureInfo.InvariantCulture ?
			NumberFormatInfo ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			using ( StreamWriter writer = EngineStorage.GetWriter(fileName, this.GetType()) )
			{
				// TODO move matrix reading and writing to the MatrixUtils class
            	writer.WriteLine(user_feature.dim1 + " " + user_feature.dim2);
            	for (int i = 0; i < user_feature.dim1; i++)
            	{
                	for (int j = 0; j < user_feature.dim2; j++)
                	{
                    	writer.WriteLine(i + " " + j + " " + user_feature.Get(i, j).ToString(ni));
                	}
            	}

            	writer.WriteLine(item_feature.dim1 + " " + item_feature.dim2);
            	for (int i = 0; i < item_feature.dim1; i++)
            	{
                	for (int j = 0; j < item_feature.dim2; j++)
                	{
                    	writer.WriteLine(i + " " + j + " " + item_feature.Get(i, j).ToString(ni));
                	}
            	}
			}
		}

		// TODO share code with MatrixFactorization
		/// <inheritdoc />
		public override void LoadModel(string filePath)
		{
			NumberFormatInfo ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

            using ( StreamReader reader = EngineStorage.GetReader(filePath, this.GetType()) )
			{
            	string[] numbers = reader.ReadLine().Split(' ');
				int num_users = System.Int32.Parse(numbers[0]);
				int dim2 = System.Int32.Parse(numbers[1]);

				max_user_id = num_users - 1;
				Matrix<double> user_feature = new Matrix<double>(num_users, dim2);
				int num_features = dim2;

            	while ((numbers = reader.ReadLine().Split(' ')).Length == 3)
            	{
					int i = System.Int32.Parse(numbers[0]);
					int j = System.Int32.Parse(numbers[1]);
					double v = System.Double.Parse(numbers[2], ni);

                	if (i >= num_users)
						throw new Exception(string.Format("Invalid user ID {0} is greater than {1}.", i, num_users - 1));
					if (j >= num_features)
						throw new Exception(string.Format("Invalid feature ID {0} is greater than {1}.", j, num_features - 1));

                	user_feature.Set(i, j, v);
				}

            	int num_items = System.Int32.Parse(numbers[0]);
				dim2 = System.Int32.Parse(numbers[1]);
				if (dim2 != num_features)
            	{
					throw new Exception("dim2 of item_feature must be feature_count");
				}
				Matrix<double> item_feature = new Matrix<double>(num_items, dim2);

            	while (!reader.EndOfStream)
            	{
					numbers = reader.ReadLine().Split(' ');
					int i = System.Int32.Parse(numbers[0]);
					int j = System.Int32.Parse(numbers[1]);
					double v = System.Double.Parse(numbers[2], ni);

                	if (i >= num_items)
						throw new Exception(string.Format("Invalid item ID {0} is greater than {1}.", i, num_items - 1));
					if (j >= num_features)
						throw new Exception(string.Format("Invalid feature ID {0} is greater than {1}.", j, num_features - 1));


					item_feature.Set(i, j, v);
				}

				// fix max_user_id and max_item_id - our model does not know more
				this.max_user_id = num_users - 1;
				this.max_item_id = num_items - 1;

            	// assign new model
				if (this.num_features != num_features)
				{
					Console.Error.WriteLine("Set num_features to {0}", num_features);
            		this.num_features = num_features;
				}
            	this.user_feature = user_feature;
            	this.item_feature = item_feature;
			}
        }
    }
}
