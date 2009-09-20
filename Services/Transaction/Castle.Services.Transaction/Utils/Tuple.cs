// Copyright 2004-2009 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.Services.Transaction
{
	using System;

	/// <summary>
	/// Vanilla tuple, 5 kr, only today!
	/// </summary>
	/// <typeparam name="TFirst"></typeparam>
	/// <typeparam name="TSecond"></typeparam>
	[Serializable]
	public struct Tuple<TFirst, TSecond>
	{
		private readonly TFirst _F;
		private readonly TSecond _S;

		public Tuple(TFirst f, TSecond s)
		{
			_F = f;
			_S = s;
		}

		public TFirst Fst
		{
			get { return _F; }
		}

		public TSecond Snd
		{
			get { return _S; }
		}
	}
}
