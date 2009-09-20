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
	using System.Collections;
	using System.ComponentModel;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Transactions;
	using Microsoft.Win32.SafeHandles;

	///<summary>
	/// Represents a transaction on transactional kernels
	/// like the Vista kernel or Server 2008 or Windows 7.
	///</summary>
	public sealed class FileTransaction : MarshalByRefObject, IFileTransaction
	{
		private readonly IDictionary _Context = new Hashtable();

		private SafeTxHandle _TransactionHandle;

		private TransactionStatus _Status = TransactionStatus.NoTransaction;
		private readonly string _Name;
		private bool _CanCommit;
		private bool _IsDistributed;
		private bool _Disposed;

		#region Constructors

		///<summary>
		/// c'tor w/o name.
		///</summary>
		public FileTransaction()
		{
		}

		///<summary>
		/// c'tor for the file transaction.
		///</summary>
		///<param name="name">The name of the transaction.</param>
		public FileTransaction(string name)
		{
			_Name = name;
		}

		#endregion

		#region ITransaction Members

		/// <summary>
		/// Starts the transaction by requesting a new transaction
		/// from the kernel transaction manager.
		/// </summary>
		public void Begin()
		{
			if (_Status == TransactionStatus.Active)
				return;

			retry:
			// we have a ongoing current transaction, join it!
			if (System.Transactions.Transaction.Current != null)
			{
				var ktx = TransactionInterop.GetDtcTransaction(System.Transactions.Transaction.Current)
				          as IKernelTransaction; // runtime should handle cast from GUID spec on interface.

				// check for race-condition.
				if (ktx == null)
					goto retry;

				IntPtr handle;
				ktx.GetHandle(out handle);
				_TransactionHandle = new SafeTxHandle(handle);

				_IsDistributed = true;
			}
			else _TransactionHandle = createTransaction(string.Format("{0} Transaction", _Name));

			if (_TransactionHandle.IsInvalid)
			{
				_Status = TransactionStatus.Invalid;

				throw new TransactionException(
					"Cannot begin file transaction because we got a null pointer back from CreateTransaction.",
					Marshal.GetExceptionForHR(Marshal.GetLastWin32Error()));
			}

			_Status = TransactionStatus.Active;
			_CanCommit = true;
		}

		public void Commit()
		{
			if (!_CanCommit)
				throw new TransactionException("Rollback only was set.");

			if (!CommitTransaction(_TransactionHandle))
			{
				_Status = TransactionStatus.Invalid;
				throw new TransactionException("Commit failed.");
			}

			_Status = TransactionStatus.Committed;
		}

		public void Rollback()
		{
			if (_Status == TransactionStatus.RolledBack)
				return;

			_Status = TransactionStatus.RolledBack;
			_CanCommit = false;

			if (!RollbackTransaction(_TransactionHandle))
				throw new TransactionException("Rollback failed. You cannot use the transaction instance further.");
		}

		public void SetRollbackOnly()
		{
			_CanCommit = false;
		}

		public void Enlist(IResource resource)
		{
			throw new NotImplementedException(
				"The file transaction doesn't support resources. Please use the standard transaction and add a resource to it.");
		}

		public void RegisterSynchronization(ISynchronization synchronization)
		{
			throw new NotImplementedException(
				"The file transaction doesn't support synchronizations. Please use the standard transaction and add a resource to it.");
		}

		public TransactionStatus Status
		{
			get { return _Status; }
		}

		public IDictionary Context
		{
			get { return _Context; }
		}

		// This isn't really relevant with the current architecture
		///<summary>
		/// Not relevant.
		///</summary>
		public bool IsChildTransaction
		{
			get { return false; }
		}

		/// <summary>
		/// Gets whether rollback only is set.
		/// </summary>
		public bool IsRollbackOnlySet
		{
			get { return !_CanCommit; }
		}

		///<summary>
		/// Gets the transaction mode of the transaction.
		///</summary>
		public TransactionMode TransactionMode
		{
			get { return TransactionMode.Unspecified; }
		}

		///<summary>
		/// Gets the isolation mode of the transaction.
		///</summary>
		public IsolationMode IsolationMode
		{
			get { return IsolationMode.RepeatableRead; }
		}

		/// <summary>
		/// Gets whether the transaction is a distributed transaction.
		/// TODO: What happens if the transaction is promoted to a distributed tx after it is created?
		/// </summary>
		public bool DistributedTransaction
		{
			get { return _IsDistributed; }
		}

		///<summary>
		/// Gets the name of the transaction.
		///</summary>
		public string Name
		{
			get { return _Name ?? string.Format("FileTransaction #{0}", GetHashCode()); }
		}

		public IResource[] Resources
		{
			get
			{
				throw new NotImplementedException(
					"The file transaction doesn't support resources. Please use the standard transaction and enlist resources on it instead.");
			}
		}

		private void assertState(TransactionStatus status)
		{
			assertState(status, null);
		}

		private void assertState(TransactionStatus status, string msg)
		{
			if (status != _Status)
			{
				if (!string.IsNullOrEmpty(msg))
					throw new TransactionException(msg);
				throw new TransactionException(string.Format("State failure; should have been {0} but was {1}",
				                                             status, _Status));
			}
		}

		#endregion

		#region IFileAdapter and IDirectoryAdapter Members

		#region AMBIGIOUS METHODS

		FileStream IFileAdapter.Create(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			assertState(TransactionStatus.Active);

			return open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
		}

		/// <summary>Creates a directory at the path given.</summary>
		///<param name="path">The path to create the directory at.</param>
		void IDirectoryAdapter.Create(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			assertState(TransactionStatus.Active);

			if (!createDirectoryTransacted(path))
				throw new TransactionException(string.Format("Failed to create directory at path {0}. See inner exception for more details.", path),
				                               new Win32Exception(Marshal.GetLastWin32Error()));
		}

		/// <summary>
		/// Deletes a file as part of a transaction
		/// </summary>
		/// <param name="filePath"></param>
		void IFileAdapter.Delete(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException("filePath");
			assertState(TransactionStatus.Active);

			if (!DeleteFileTransactedW(filePath, _TransactionHandle))
				throw new TransactionException("Unable to perform transacted file delete.",
				                               new Win32Exception(Marshal.GetLastWin32Error()));
		}

		/// <summary>
		/// Deletes a folder recursively.
		/// </summary>
		/// <param name="path">The directory path to start deleting at!</param>
		void IDirectoryAdapter.Delete(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			assertState(TransactionStatus.Active);

			if (!RemoveDirectoryTransactedW(path, _TransactionHandle))
				throw new TransactionException("Unable to delete folder. See inner exception for details.",
				                               new Win32Exception(Marshal.GetLastWin32Error()));
		}

		bool IFileAdapter.Exists(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException("filePath");
			assertState(TransactionStatus.Active);

			using (var handle = findFirstFileTransacted(filePath, false))
				return !handle.IsInvalid;
		}

		/// <summary>
		/// Checks whether the path exists.
		/// </summary>
		/// <param name="path">Path to check.</param>
		/// <returns>True if it exists, false otherwise.</returns>
		bool IDirectoryAdapter.Exists(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
			assertState(TransactionStatus.Active);

			if (path.EndsWith("\\"))
				path = path.Substring(0, path.Length - 1);

			using (var handle = findFirstFileTransacted(path, true))
				return !handle.IsInvalid;
		}

		string IDirectoryAdapter.GetFullPath(string dir)
		{
			if (dir == null) throw new ArgumentNullException("dir");
			assertState(TransactionStatus.Active);

			return getFullPathNameTransacted(dir);
		}

		public string MapPath(string path)
		{
			throw new NotImplementedException("Implemented on the directory adapter.");
		}

		void IDirectoryAdapter.Move(string originalPath, string newPath)
		{
			throw new NotImplementedException();
		}

		#endregion

		public FileStream Open(string filePath, FileMode mode)
		{
			if (filePath == null) throw new ArgumentNullException("filePath");

			return open(filePath, mode, FileAccess.ReadWrite, FileShare.None);
		}

		/// <summary>
		/// Implemented in the file adapter.
		/// </summary>
		int IFileAdapter.WriteStream(string toFilePath, Stream fromStream)
		{
			throw new NotImplementedException("Use the file adapter instead!!");
		}

		public string ReadAllText(string path, Encoding encoding)
		{
			assertState(TransactionStatus.Active);

			using (var reader = new StreamReader(open(path, FileMode.Open, FileAccess.Read, FileShare.Read), encoding))
			{
				return reader.ReadToEnd();
			}
		}

		void IFileAdapter.Move(string originalFilePath, string newFilePath)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Reads all text from a file as part of a transaction
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public string ReadAllText(string path)
		{
			assertState(TransactionStatus.Active);

			using (var reader = new StreamReader(open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
			{
				return reader.ReadToEnd();
			}
		}

		/// <summary>
		/// Writes text to a file as part of a transaction
		/// </summary>
		/// <param name="path"></param>
		/// <param name="contents"></param>
		public void WriteAllText(string path, string contents)
		{
			assertState(TransactionStatus.Active);

			using (var writer = new StreamWriter(open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)))
			{
				writer.Write(contents);
			}
		}

		#endregion

		#region IDirectoryAdapter

		/// <summary>
		/// Deletes a folder.
		/// </summary>
		/// <param name="path">The path to the folder to delete.</param>
		/// <param name="recursively">
		/// Whether to delete recursively or not.
		/// When recursive, we delete all subfolders and files in the given
		/// directory as well.
		/// </param>
		bool IDirectoryAdapter.Delete(string path, bool recursively)
		{
			assertState(TransactionStatus.Active);
			return recursively ? deleteRecursive(path) : RemoveDirectoryTransactedW(path, _TransactionHandle);
		}

		#endregion

		#region Dispose-pattern

		///<summary>
		/// Gets whether the transaction is disposed.
		///</summary>
		public bool IsDisposed
		{
			get { return _Disposed; }
		}

		~FileTransaction()
		{
			dispose(false);
		}

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (_Disposed) return;

			if (disposing)
			{
				// called via the Dispose() method on IDisposable, 
				// can use private object references.

				if (_TransactionHandle != null)
				{
					if (!_TransactionHandle.IsClosed)
						_TransactionHandle.Close();
	
					_TransactionHandle.Dispose();
				}
			}

			_Disposed = true;
		}

		#endregion

		#region C++ Interop
		// ReSharper disable InconsistentNaming
		// ReSharper disable UnusedMember.Local

		// overview here: http://msdn.microsoft.com/en-us/library/aa964885(VS.85).aspx
		// helper: http://www.improve.dk/blog/2009/02/14/utilizing-transactional-ntfs-through-dotnet

		/// <summary>
		/// Creates a file handle with the current ongoing transaction.
		/// </summary>
		/// <param name="path">The path of the file.</param>
		/// <param name="mode">The file mode, i.e. what is going to be done if it exists etc.</param>
		/// <param name="access">The access rights this handle has.</param>
		/// <param name="share">What other handles may be opened; sharing settings.</param>
		/// <returns>A safe file handle. Not null, but may be invalid.</returns>
		private FileStream open(
			string path, FileMode mode, FileAccess access, FileShare share)
		{
			// TODO: Support System.IO.FileOptinons which is the dwFlagsAndAttribute parameter.
			var fileHandle = CreateFileTransactedW(path,
			                                       translateFileAccess(access),
			                                       translateFileShare(share),
			                                       IntPtr.Zero,
			                                       translateFileMode(mode),
			                                       0, IntPtr.Zero,
			                                       _TransactionHandle,
			                                       IntPtr.Zero, IntPtr.Zero);


			if (fileHandle.IsInvalid)
				throw new Win32Exception(Marshal.GetLastWin32Error());

			return new FileStream(fileHandle, access);
		}

		[Serializable]
		private enum NativeFileMode : uint
		{
			CREATE_NEW = 1,
			CREATE_ALWAYS = 2,
			OPEN_EXISTING = 3,
			OPEN_ALWAYS = 4,
			TRUNCATE_EXISTING = 5
		}

		[Flags, Serializable]
		private enum NativeFileAccess : uint
		{
			GenericRead = 0x80000000,
			GenericWrite = 0x40000000
		}

		/// <summary>
		/// The sharing mode of an object, which can be read, write, both, delete, all of these, or none (refer to the following table).
		/// If this parameter is zero and CreateFileTransacted succeeds, the object cannot be shared and cannot be opened again until the handle is closed. For more information, see the Remarks section of this topic.
		/// You cannot request a sharing mode that conflicts with the access mode that is specified in an open request that has an open handle, because that would result in the following sharing violation: ERROR_SHARING_VIOLATION. For more information, see Creating and Opening Files.
		/// </summary>
		[Flags, Serializable]
		private enum NativeFileShare : uint
		{
			/// <summary>
			/// Disables subsequent open operations on an object to request any type of access to that object.
			/// </summary>
			None = 0x00,

			/// <summary>
			/// Enables subsequent open operations on an object to request read access.
			/// Otherwise, other processes cannot open the object if they request read access.
			/// If this flag is not specified, but the object has been opened for read access, the function fails.
			/// </summary>
			Read = 0x01,

			/// <summary>
			/// Enables subsequent open operations on an object to request write access.
			/// Otherwise, other processes cannot open the object if they request write access.
			/// If this flag is not specified, but the object has been opened for write access or has a file mapping with write access, the function fails.
			/// </summary>
			Write = 0x02,

			/// <summary>
			/// Enables subsequent open operations on an object to request delete access.
			/// Otherwise, other processes cannot open the object if they request delete access.
			/// If this flag is not specified, but the object has been opened for delete access, the function fails.
			/// </summary>
			Delete = 0x04
		}

		/// <summary>
		/// Managed -> Native mapping
		/// </summary>
		/// <param name="mode">The filemode to translate.</param>
		/// <returns>The native file mode.</returns>
		private static NativeFileMode translateFileMode(FileMode mode)
		{
			if (mode != FileMode.Append)
				return (NativeFileMode) (uint) mode;
			return (NativeFileMode) (uint) FileMode.OpenOrCreate;
		}

		/// <summary>
		/// Managed -> Native mapping
		/// </summary>
		/// <param name="access"></param>
		/// <returns></returns>
		private static NativeFileAccess translateFileAccess(FileAccess access)
		{
			switch (access)
			{
				case FileAccess.Read:
					return NativeFileAccess.GenericRead;
				case FileAccess.Write:
					return NativeFileAccess.GenericWrite;
				case FileAccess.ReadWrite:
					return NativeFileAccess.GenericRead | NativeFileAccess.GenericWrite;
				default:
					throw new ArgumentOutOfRangeException("access");
			}
		}

		/// <summary>
		/// Direct Managed -> Native mapping
		/// </summary>
		/// <param name="share"></param>
		/// <returns></returns>
		private static NativeFileShare translateFileShare(FileShare share)
		{
			return (NativeFileShare) (uint) share;
		}

#pragma warning disable 1591

		///<summary>
		/// Attributes for security interop.
		///</summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct SECURITY_ATTRIBUTES
		{
			public int nLength;
			public IntPtr lpSecurityDescriptor;
			public int bInheritHandle;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern SafeFileHandle CreateFileW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
			NativeFileAccess dwDesiredAccess,
			NativeFileShare dwShareMode,
			IntPtr lpSecurityAttributes,
			NativeFileMode dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile);


		#region Files

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern SafeFileHandle CreateFileTransactedW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
			NativeFileAccess dwDesiredAccess,
			NativeFileShare dwShareMode,
			IntPtr lpSecurityAttributes,
			NativeFileMode dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile,
			SafeTxHandle hTransaction,
			IntPtr pusMiniVersion,
			IntPtr pExtendedParameter);

		/// <summary>
		/// http://msdn.microsoft.com/en-us/library/aa363916(VS.85).aspx
		/// </summary>
		[DllImport("kernel32", SetLastError = true)]
		private static extern bool DeleteFileTransactedW(
			[MarshalAs(UnmanagedType.LPWStr)] string file,
			SafeTxHandle transaction);

		#endregion

		#region Directories

		/// <summary>
		/// http://msdn.microsoft.com/en-us/library/aa363857(VS.85).aspx
		/// Creates a new directory as a transacted operation, with the attributes of a specified 
		/// template directory. If the underlying file system supports security on files and 
		/// directories, the function applies a specified security descriptor to the new directory. 
		/// The new directory retains the other attributes of the specified template directory.
		/// </summary>
		/// <param name="lpTemplateDirectory">
		/// The path of the directory to use as a template 
		/// when creating the new directory. This parameter can be NULL.
		/// </param>
		/// <param name="lpNewDirectory">The path of the directory to be created. </param>
		/// <param name="lpSecurityAttributes">A pointer to a SECURITY_ATTRIBUTES structure. The lpSecurityDescriptor member of the structure specifies a security descriptor for the new directory.</param>
		/// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
		/// <returns>True if the call succeeds, otherwise do a GetLastError.</returns>
		[DllImport("kernel32", SetLastError = true)]
		private static extern bool CreateDirectoryTransactedW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpTemplateDirectory,
			[MarshalAs(UnmanagedType.LPWStr)] string lpNewDirectory,
			IntPtr lpSecurityAttributes,
			SafeTxHandle hTransaction);

		private bool createDirectoryTransacted(string templatePath,
		                                       string dirPath)
		{
			return CreateDirectoryTransactedW(templatePath,
			                                  dirPath,
			                                  IntPtr.Zero,
			                                  _TransactionHandle);
		}

		private bool createDirectoryTransacted(string dirPath)
		{
			return createDirectoryTransacted(null, dirPath);
		}

		/// <summary>
		/// http://msdn.microsoft.com/en-us/library/aa365490(VS.85).aspx
		/// Deletes an existing empty directory as a transacted operation.
		/// </summary>
		/// <param name="lpPathName">
		/// The path of the directory to be removed. 
		/// The path must specify an empty directory, 
		/// and the calling process must have delete access to the directory.
		/// </param>
		/// <param name="hTransaction">A handle to the transaction. This handle is returned by the CreateTransaction function.</param>
		/// <returns>True if the call succeeds, otherwise do a GetLastError.</returns>
		[DllImport("kernel32", SetLastError = true)]
		private static extern bool RemoveDirectoryTransactedW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpPathName,
			SafeTxHandle hTransaction);

		/// <summary>
		/// http://msdn.microsoft.com/en-us/library/aa364966(VS.85).aspx
		/// Retrieves the full path and file name of the specified file as a transacted operation.
		/// </summary>
		/// <remarks>
		/// GetFullPathNameTransacted merges the name of the current drive and directory 
		/// with a specified file name to determine the full path and file name of a 
		/// specified file. It also calculates the address of the file name portion of
		/// the full path and file name. This function does not verify that the 
		/// resulting path and file name are valid, or that they see an existing file 
		/// on the associated volume.
		/// </remarks>
		/// <param name="lpFileName">The name of the file. The file must reside on the local computer; 
		/// otherwise, the function fails and the last error code is set to 
		/// ERROR_TRANSACTIONS_UNSUPPORTED_REMOTE.</param>
		/// <param name="nBufferLength">The size of the buffer to receive the null-terminated string for the drive and path, in TCHARs. </param>
		/// <param name="lpBuffer">A pointer to a buffer that receives the null-terminated string for the drive and path.</param>
		/// <param name="lpFilePart">A pointer to a buffer that receives the address (in lpBuffer) of the final file name component in the path. 
		/// Specify NULL if you do not need to receive this information.
		/// If lpBuffer points to a directory and not a file, lpFilePart receives 0 (zero).</param>
		/// <param name="hTransaction"></param>
		/// <returns>If the function succeeds, the return value is the length, in TCHARs, of the string copied to lpBuffer, not including the terminating null character.</returns>
		[DllImport( "Kernel32.dll", CharSet=CharSet.Auto, SetLastError = true)]
		private static extern int GetFullPathNameTransactedW(
			[In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
			[In] int nBufferLength,
			[Out] StringBuilder lpBuffer,
			[In, Out] ref IntPtr lpFilePart,
			[In] SafeTxHandle hTransaction);

		/*
		 * Might need to use:
		 * DWORD WINAPI GetLongPathNameTransacted(
		 *	  __in   LPCTSTR lpszShortPath,
		 *	  __out  LPTSTR lpszLongPath,
		 *	  __in   DWORD cchBuffer,
		 *	  __in   HANDLE hTransaction
		 *	);
		 */
		private string getFullPathNameTransacted(string dirOrFilePath)
		{
			var sb = new StringBuilder(512);

			retry:
			var p = IntPtr.Zero;
			int res = GetFullPathNameTransactedW(dirOrFilePath,
			                                     sb.Capacity,
			                                     sb,
			                                     ref p, // here we can check if it's a file or not.
			                                     _TransactionHandle);

			if (res == 0) // failure
			{
				Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
			}
			if (res > sb.Capacity)
			{
				sb.Capacity = res;
				goto retry; // handle edge case if the path.Length > 512.
			}
			return sb.ToString();
		} 
		// more examples in C++:  
		// http://msdn.microsoft.com/en-us/library/aa364963(VS.85).aspx
		// http://msdn.microsoft.com/en-us/library/x3txb6xc.aspx
		// http://www.csharphelp.com/archives/archive63.html - marshalling strange types
				
		// The CharSet must match the CharSet of the corresponding PInvoke signature
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		struct WIN32_FIND_DATA
		{
			public uint dwFileAttributes;
			public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
			public uint nFileSizeHigh;
			public uint nFileSizeLow;
			public uint dwReserved0;
			public uint dwReserved1;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string cFileName;
			
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			public string cAlternateFileName;
		}

		[Serializable]
		private enum FINDEX_INFO_LEVELS
		{
			FindExInfoStandard = 0,
			FindExInfoMaxInfoLevel = 1
		}

		[Serializable]
		private enum FINDEX_SEARCH_OPS
		{
			FindExSearchNameMatch = 0,
			FindExSearchLimitToDirectories = 1,
			FindExSearchLimitToDevices = 2,
			FindExSearchMaxSearchOp = 3
		}

/*
 * HANDLE WINAPI FindFirstFileTransacted(
  __in        LPCTSTR lpFileName,
  __in        FINDEX_INFO_LEVELS fInfoLevelId,
  __out       LPVOID lpFindFileData,
  __in        FINDEX_SEARCH_OPS fSearchOp,
  __reserved  LPVOID lpSearchFilter,
  __in        DWORD dwAdditionalFlags,
  __in        HANDLE hTransaction
);
*/
		/// <summary>
		/// 
		/// </summary>
		/// <param name="lpFileName"></param>
		/// <param name="fInfoLevelId"></param>
		/// <param name="lpFindFileData"></param>
		/// <param name="fSearchOp">The type of filtering to perform that is different from wildcard matching.</param>
		/// <param name="lpSearchFilter">
		/// A pointer to the search criteria if the specified fSearchOp needs structured search information.
		/// At this time, none of the supported fSearchOp values require extended search information. Therefore, this pointer must be NULL.
		/// </param>
		/// <param name="dwAdditionalFlags">
		/// Specifies additional flags that control the search.
		/// FIND_FIRST_EX_CASE_SENSITIVE = 0x1
		/// Means: Searches are case-sensitive.
		/// </param>
		/// <param name="hTransaction"></param>
		/// <returns></returns>
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern SafeFindHandle FindFirstFileTransactedW(
			[In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
			[In] FINDEX_INFO_LEVELS fInfoLevelId, // TODO: Won't work.
			[Out] out WIN32_FIND_DATA lpFindFileData,
			[In] FINDEX_SEARCH_OPS fSearchOp,
			IntPtr lpSearchFilter,
			[In] uint dwAdditionalFlags,
			[In] SafeTxHandle hTransaction);

		private SafeFindHandle FindFirstFileTransactedW(string lpFileName,
		                                                out WIN32_FIND_DATA lpFindFileData)
		{
			return FindFirstFileTransactedW(lpFileName, FINDEX_INFO_LEVELS.FindExInfoStandard,
			                                out lpFindFileData, 
			                                FINDEX_SEARCH_OPS.FindExSearchNameMatch, 
			                                IntPtr.Zero, 0,
			                                _TransactionHandle);
		}

		private SafeFindHandle findFirstFileTransacted(string filePath, bool directory)
		{
			WIN32_FIND_DATA data;

#if MONO
			uint caseSensitive = 0x1;
#else 
			uint caseSensitive = 0;
#endif

			// TODO: Use "\\?\" to be sure to be able to search deep folder hierarchies. ( >260 chars )
			return FindFirstFileTransactedW(filePath,
			                                FINDEX_INFO_LEVELS.FindExInfoStandard, out data,
			                                directory
			                                	? FINDEX_SEARCH_OPS.FindExSearchLimitToDirectories
			                                	: FINDEX_SEARCH_OPS.FindExSearchNameMatch,
			                                IntPtr.Zero, caseSensitive, _TransactionHandle);
		}

		#region Unused so far

		private bool deleteRecursive(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (path == string.Empty) throw new ArgumentException("You can't pass an empty string.");

			WIN32_FIND_DATA findData;
			bool addPrefix = !path.StartsWith(@"\\?\");
			bool ok = true;

			string pathWithoutSufflix = addPrefix ? @"\\?\" + PathUtil.GetFullPath(path) : PathUtil.GetFullPath(path);
			path = pathWithoutSufflix + "\\*";
			

			using (var findHandle = FindFirstFileTransactedW(path, out findData))
			{
				if (findHandle.IsInvalid) return false;

				do
				{
					var subPath = pathWithoutSufflix.Combine(findData.cFileName);

					if ((findData.dwFileAttributes & (uint) FileAttributes.Directory) != 0)
					{
						if (findData.cFileName != "." && findData.cFileName != "..")
							ok &= deleteRecursive(subPath);
					}
					else
						ok = ok && DeleteFileTransactedW(subPath, _TransactionHandle);
				}
				while (FindNextFile(findHandle, out findData));
			}

			return ok && RemoveDirectoryTransactedW(pathWithoutSufflix, _TransactionHandle);
		}

		// not tx
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern SafeFindHandle FindFirstFile(string lpFileName, 
		                                           out WIN32_FIND_DATA lpFindFileData);

		// not tx.
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool FindNextFile(SafeHandle hFindFile, 
		                                        out WIN32_FIND_DATA lpFindFileData);

		// Example
		public long RecurseDirectory(string directory, int level, out int files, out int folders)
		{
			IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
			long size = 0;
			files = 0;
			folders = 0;
			WIN32_FIND_DATA findData;

			// please note that the following line won't work if you try this on a network folder, like \\Machine\C$
			// simply remove the \\?\ part in this case or use \\?\UNC\ prefix
			using (SafeFindHandle findHandle = FindFirstFile(@"\\?\" + directory + @"\*", out findData))
			{
				if (!findHandle.IsInvalid)
				{

					do
					{
						if ((findData.dwFileAttributes & (uint)FileAttributes.Directory) != 0)
						{

							if (findData.cFileName != "." && findData.cFileName != "..")
							{
								folders++;

								int subfiles, subfolders;
								string subdirectory = directory + (directory.EndsWith(@"\") ? "" : @"\") +
								                      findData.cFileName;
								if (level != 0)  // allows -1 to do complete search.
								{
									size += RecurseDirectory(subdirectory, level - 1, out subfiles, out subfolders);

									folders += subfolders;
									files += subfiles;
								}
							}
						}
						else
						{
							// File
							files++;

							size += (long)findData.nFileSizeLow + (long)findData.nFileSizeHigh * 4294967296;
						}
					}
					while (FindNextFile(findHandle, out findData));
				}

			}

			return size;
		}

		#endregion

		#endregion

		#region Kernel transaction manager

		/// <summary>
		/// Creates a new transaction object.
		/// </summary>
		/// <remarks>
		/// Don't pass unicode to the description (there's no Wide-version of this function
		/// in the kernel).
		/// </remarks>
		/// <param name="lpTransactionAttributes">    
		/// A pointer to a SECURITY_ATTRIBUTES structure that determines whether the returned handle 
		/// can be inherited by child processes. If this parameter is NULL, the handle cannot be inherited.
		/// The lpSecurityDescriptor member of the structure specifies a security descriptor for 
		/// the new event. If lpTransactionAttributes is NULL, the object gets a default 
		/// security descriptor. The access control lists (ACL) in the default security 
		/// descriptor for a transaction come from the primary or impersonation token of the creator.
		/// </param>
		/// <param name="uow">Reserved. Must be zero (0).</param>
		/// <param name="createOptions">
		/// Any optional transaction instructions. 
		/// Value:		TRANSACTION_DO_NOT_PROMOTE
		/// Meaning:	The transaction cannot be distributed.
		/// </param>
		/// <param name="isolationLevel">Reserved; specify zero (0).</param>
		/// <param name="isolationFlags">Reserved; specify zero (0).</param>
		/// <param name="timeout">    
		/// The time, in milliseconds, when the transaction will be aborted if it has not already 
		/// reached the prepared state.
		/// Specify NULL to provide an infinite timeout.
		/// </param>
		/// <param name="description">A user-readable description of the transaction.</param>
		/// <returns>
		/// If the function succeeds, the return value is a handle to the transaction.
		/// If the function fails, the return value is INVALID_HANDLE_VALUE.
		/// </returns>
		[DllImport("ktmw32.dll", SetLastError = true)]
		private static extern IntPtr CreateTransaction(
			IntPtr lpTransactionAttributes,
			IntPtr uow,
			uint createOptions,
			uint isolationLevel,
			uint isolationFlags,
			uint timeout,
			string description);

		private static SafeTxHandle createTransaction(string description)
		{
			return new SafeTxHandle(CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 0, description));
		}

		/// <summary>
		/// Requests that the specified transaction be committed.
		/// </summary>
		/// <remarks>You can commit any transaction handle that has been opened 
		/// or created using the TRANSACTION_COMMIT permission; any application can 
		/// commit a transaction, not just the creator.
		/// This function can only be called if the transaction is still active, 
		/// not prepared, pre-prepared, or rolled back.</remarks>
		/// <param name="transaction">
		/// This handle must have been opened with the TRANSACTION_COMMIT access right. 
		/// For more information, see KTM Security and Access Rights.</param>
		/// <returns></returns>
		[DllImport("ktmw32.dll", SetLastError = true)]
		private static extern bool CommitTransaction(SafeTxHandle transaction);

		/// <summary>
		/// Requests that the specified transaction be rolled back. This function is synchronous.
		/// </summary>
		/// <param name="transaction">A handle to the transaction.</param>
		/// <returns>If the function succeeds, the return value is nonzero.</returns>
		[DllImport("ktmw32.dll", SetLastError = true)]
		private static extern bool RollbackTransaction(SafeTxHandle transaction);

		#endregion

		// ReSharper restore UnusedMember.Local
		// ReSharper restore InconsistentNaming
#pragma warning restore 1591

		#endregion
	}
}
