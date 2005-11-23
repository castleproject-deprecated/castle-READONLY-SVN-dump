// Copyright 2004-2005 Castle Project - http://www.castleproject.org/
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

namespace Castle.MonoRail.Framework.Extensions.ExceptionChaining
{
	using System;
	using System.Xml;

	using Castle.MonoRail.Framework.Configuration;

	/// <summary>
	/// 
	/// </summary>
	public class ExceptionChainingExtension : AbstractMonoRailExtension
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="configuration"></param>
		public override void Init(MonoRailConfiguration configuration)
		{
			XmlNodeList handlers = configuration.ConfigSection.SelectNodes("exception/exceptionHandler");

			foreach(XmlNode node in handlers)
			{
				XmlAttribute typeAtt = node.Attributes["type"];

				if (typeAtt == null)
				{
					// TODO: Throw configuration exception
				}

				InstallExceptionHandler(node, typeAtt.Value);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		public override void OnActionException(IRailsEngineContext context)
		{
			base.OnActionException(context);
		}

		private void InstallExceptionHandler(XmlNode node, string value)
		{
		}
	}
}
