// Copyright 2004-2008 Castle Project - http://www.castleproject.org/
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

namespace Castle.Windsor.Configuration.Interpreters
{
	using System;
	using System.Xml;
	using System.Configuration;
	using Castle.Core.Configuration.Xml;
	using Castle.Core.Resource;
	using Castle.Core.Configuration;
	
	using Castle.MicroKernel;
	using Castle.MicroKernel.SubSystems.Resource;

	using Castle.Windsor.Configuration.Interpreters.XmlProcessor;

	/// <summary>
	/// Reads the configuration from a XmlFile. Sample structure:
	/// <code>
	/// &lt;configuration&gt;
	///   &lt;facilities&gt;
	///     &lt;facility id="myfacility"&gt;
	///     
	///     &lt;/facility&gt;
	///   &lt;/facilities&gt;
	///   
	///   &lt;components&gt;
	///     &lt;component id="component1"&gt;
	///     
	///     &lt;/component&gt;
	///   &lt;/components&gt;
	/// &lt;/configuration&gt;
	/// </code>
	/// </summary>
	public class XmlInterpreter : AbstractInterpreter
	{
		private IKernel kernel;

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="XmlInterpreter"/> class.
		/// </summary>
		public XmlInterpreter()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="XmlInterpreter"/> class.
		/// </summary>
		/// <param name="filename">The filename.</param>
		public XmlInterpreter(String filename) : base(filename)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="XmlInterpreter"/> class.
		/// </summary>
		/// <param name="source">The source.</param>
		public XmlInterpreter(Castle.Core.Resource.IResource source) : base(source)
		{
		}

		#endregion

		/// <summary>
		/// Gets or sets the kernel.
		/// </summary>
		/// <value>The kernel.</value>
		public IKernel Kernel
		{
			get { return kernel ; }
			set { kernel = value; }
		}

		public override void ProcessResource(IResource source, IConfigurationStore store)
		{
			XmlProcessor.XmlProcessor processor = (kernel == null) ?
				new XmlProcessor.XmlProcessor(EnvironmentName) :
				new XmlProcessor.XmlProcessor(EnvironmentName, 
					kernel.GetSubSystem(SubSystemConstants.ResourceKey) as IResourceSubSystem);

			try
			{
				XmlNode element = processor.Process(source);

				Deserialize(element, store);				
			}
			catch(XmlProcessorException)
			{
				string message = "Unable to process xml resource ";

				throw new ConfigurationErrorsException(message);
			}
		}

		protected void Deserialize(XmlNode section, IConfigurationStore store)
		{
			foreach(XmlNode node in section)
			{
				if (XmlConfigurationDeserializer.IsTextNode(node))
				{
					string message = String.Format("{0} cannot contain text nodes", node.Name);

					throw new ConfigurationErrorsException(message);
				}
				else if (node.NodeType == XmlNodeType.Element)
				{
					DeserializeElement(node, store);
				}
			}
		}

		private void DeserializeElement(XmlNode node, IConfigurationStore store)
		{
			if (ContainersNodeName.Equals(node.Name))
			{
				DeserializeContainers(node.ChildNodes, store);
			}
			else if (FacilitiesNodeName.Equals(node.Name))
			{
				DeserializeFacilities(node.ChildNodes, store);
			}
			else if (ComponentsNodeName.Equals(node.Name))
			{
				DeserializeComponents(node.ChildNodes, store);
			}
			else if (BootstrapNodeName.Equals(node.Name))
			{
				DeserializeBootstrapComponents(node.ChildNodes, store);
			}
			else
			{
				string message = string.Format(
					"Configuration parser encountered <{0}>, but it was expecting to find " +
					"<{1}>, <{2}>, <{3}> or <{4}>. There might be either a typo on <{0}> or " +
					"you might have forgotten to nest it properly.",
					node.Name, ContainersNodeName, FacilitiesNodeName, ComponentsNodeName, BootstrapNodeName);
				throw new ConfigurationErrorsException(message);
			}
		}

		private void DeserializeContainers(XmlNodeList nodes, IConfigurationStore store)
		{
			foreach(XmlNode node in nodes)
			{
				if (node.NodeType == XmlNodeType.Element)
				{
					AssertNodeName(node, ContainerNodeName);

					DeserializeContainer(node, store);
				}
			}
		}

		private void DeserializeContainer(XmlNode node, IConfigurationStore store)
		{
			String name = GetRequiredAttributeValue(node, "name");

			IConfiguration config = XmlConfigurationDeserializer.GetDeserializedNode(node);
			IConfiguration newConfig = new MutableConfiguration(config.Name, node.InnerXml);

			// Copy all attributes
			string[] allKeys = config.Attributes.AllKeys;
			
			foreach(string key in allKeys)
			{
				newConfig.Attributes.Add(key, config.Attributes[key]);
			}

			// Copy all children
			newConfig.Children.AddRange(config.Children);

			AddChildContainerConfig(name, newConfig, store);
		}

		private void DeserializeFacilities(XmlNodeList nodes, IConfigurationStore store)
		{
			foreach(XmlNode node in nodes)
			{
				if (node.NodeType == XmlNodeType.Element)
				{
					AssertNodeName(node, FacilityNodeName);

					DeserializeFacility(node, store);
				}
			}
		}

		private void DeserializeFacility(XmlNode node, IConfigurationStore store)
		{
			String id = GetRequiredAttributeValue(node, "id");

			IConfiguration config = XmlConfigurationDeserializer.GetDeserializedNode(node);

			AddFacilityConfig(id, config, store);
		}

		private void DeserializeComponents(XmlNodeList nodes, IConfigurationStore store)
		{
			foreach(XmlNode node in nodes)
			{
				if (node.NodeType == XmlNodeType.Element)
				{
					AssertNodeName(node, ComponentNodeName);

					DeserializeComponent(node, store);
				}
			}
		}

		private void DeserializeBootstrapComponents(XmlNodeList nodes, IConfigurationStore store)
		{
			foreach(XmlNode node in nodes)
			{
				if (node.NodeType == XmlNodeType.Element)
				{
					AssertNodeName(node, ComponentNodeName);

					DeserializeBootstrapComponent(node, store);
				}
			}
		}

		private void DeserializeComponent(XmlNode node, IConfigurationStore store)
		{
			String id = GetRequiredAttributeValue(node, "id");

			IConfiguration config = XmlConfigurationDeserializer.GetDeserializedNode(node);

			AddComponentConfig(id, config, store);
		}

		private void DeserializeBootstrapComponent(XmlNode node, IConfigurationStore store)
		{
			String id = GetRequiredAttributeValue(node, "id");

			IConfiguration config = XmlConfigurationDeserializer.GetDeserializedNode(node);

			AddBootstrapComponentConfig(id, config, store);
		}

		private String GetRequiredAttributeValue(XmlNode node, String attName)
		{
			String value = GetAttributeValue(node, attName);

			if (value.Length == 0)
			{
				String message = String.Format("{0} elements expects required non blank attribute {1}",
				                               node.Name, attName);

				throw new ConfigurationErrorsException(message);
			}

			return value;
		}

		private String GetAttributeValue(XmlNode node, String attName)
		{
			XmlAttribute att = node.Attributes[attName];

			return (att == null) ? String.Empty : att.Value.Trim();
		}

		private void AssertNodeName(XmlNode node, string expectedName)
		{
			if (!expectedName.Equals(node.Name))
			{
				String message = String.Format("Unexpected node under '{0}': Expected '{1}' but found '{2}'",
				                               expectedName, expectedName, node.Name);

				throw new ConfigurationErrorsException(message);
			}
		}
	}
}
