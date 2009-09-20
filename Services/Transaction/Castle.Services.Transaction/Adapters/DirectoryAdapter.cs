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
	using System.IO;
	using Castle.Core.Logging;

	/// <summary>
	/// Adapter which wraps the functionality in <see cref="File"/>
	/// together with native kernel transactions.
	/// </summary>
	public sealed class DirectoryAdapter : TxAdapterBase, IDirectoryAdapter
	{
		private readonly IMapPath _PathFinder;
		private readonly ILogger _Logger;

		///<summary>
		/// c'tor for the Directory Adapter.
		///</summary>
		///<param name="pathFinder"></param>
		///<param name="logger">Logger for logging events in the directory adapter.</param>
		///<param name="constrainToSpecifiedDir">
		/// Whether to constrain the adapter to a specific directory, specified next.
		/// If not, the next argument doesn't matter.</param>
		///<param name="specifiedDir">The specified directory to limit operations to.</param>
		public DirectoryAdapter(IMapPath pathFinder, ILogger logger, 
			bool constrainToSpecifiedDir, string specifiedDir)
			: base(constrainToSpecifiedDir, specifiedDir)
		{
			if (pathFinder == null) throw new ArgumentNullException("pathFinder");
			if (logger == null) throw new ArgumentNullException("logger");

			_Logger = logger;
			_Logger.Debug("DirectoryAdapter created.");

			_PathFinder = pathFinder;
		}

		public void Create(string path)
		{
			AssertAllowed(path);

#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
			{
				((IDirectoryAdapter)tx).Create(path);
				return;
			}
#endif

			Directory.CreateDirectory(path);
		}

		public bool Exists(string path)
		{
			AssertAllowed(path);
#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
				return ((IDirectoryAdapter)tx).Exists(path);
#endif

			return Directory.Exists(path);
		}

		/// <summary>
		/// Deletes a folder recursively.
		/// </summary>
		/// <param name="path"></param>
		public void Delete(string path)
		{
			AssertAllowed(path);
		#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
			{
				((IDirectoryAdapter)tx).Delete(path);
				return;
			}
		#endif
			Directory.Delete(path);
		}

		/// <summary>
		/// Deletes a folder.
		/// </summary>
		/// <param name="path">The path to the folder to delete.</param>
		/// <param name="recursively">
		/// Whether to delete recursively or not.
		/// When recursive, we delete all subfolders and files in the given
		/// directory as well.
		/// </param>
		public bool Delete(string path, bool recursively)
		{
			AssertAllowed(path);
#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
			{
				return tx.Delete(path, recursively);
			}
#endif
			Directory.Delete(path, recursively);
			return true;
		}

		public string GetFullPath(string path)
		{
			AssertAllowed(path);
#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
				return (tx).GetFullPath(path);
#endif
			return Path.GetFullPath(path);
		}

		public string MapPath(string path)
		{
			return _PathFinder.MapPath(path);
		}

		public void Move(string originalPath, string newPath)
		{
			AssertAllowed(originalPath);
			AssertAllowed(newPath);

#if !MONO
			IFileTransaction tx;
			if (Environment.OSVersion.Version.Major > 5 && HasTransaction(out tx))
			{
				(tx as IDirectoryAdapter).Move(originalPath, newPath);
				return;
			}
#endif

			Directory.Move(originalPath, newPath);
		}
	}
}
