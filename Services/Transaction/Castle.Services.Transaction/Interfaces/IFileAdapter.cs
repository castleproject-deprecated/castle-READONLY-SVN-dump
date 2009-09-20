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
	using System.IO;
	using System.Text;

	///<summary>
	/// File helper wrapper interface
	///</summary>
	public interface IFileAdapter
	{
		///<summary>
		/// Create a new file transactionally.
		///</summary>
		///<param name="filePath">The path, where to create the file.</param>
		///<returns>A file stream pointing to the file.</returns>
		FileStream Create(string filePath);

		///<summary>
		/// Returns whether the specified file exists or not.
		///</summary>
		///<param name="filePath">The file path.</param>
		///<returns></returns>
		bool Exists(string filePath);

		/// <summary>
		/// Reads all text from a file as part of a transaction
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		string ReadAllText(string path);

		/// <summary>
		/// Writes text to a file as part of a transaction
		/// </summary>
		/// <param name="path"></param>
		/// <param name="contents"></param>
		void WriteAllText(string path, string contents);

		/// <summary>
		/// Deletes a file as part of a transaction
		/// </summary>
		/// <param name="filePath"></param>
		void Delete(string filePath);

		/// <summary>
		/// Opens a file with RW access.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="mode">The file mode, which specifies </param>
		/// <returns></returns>
		FileStream Open(string filePath, FileMode mode);

		/// <summary>
		/// Writes an input stream to the file path.
		/// </summary>
		/// <param name="toFilePath">The path to write to.</param>
		/// <param name="fromStream">The stream to read from.</param>
		/// <returns>The number of bytes written.</returns>
		int WriteStream(string toFilePath, Stream fromStream);

		///<summary>
		/// Reads all text in a file and returns the string of it.
		///</summary>
		///<param name="path"></param>
		///<param name="encoding"></param>
		///<returns></returns>
		string ReadAllText(string path, Encoding encoding);

		///<summary>
		/// Moves a file.
		///</summary>
		///<param name="originalFilePath">
		/// The original file path. It can't be null nor can it point to a directory.
		/// </param>
		///<param name="newFilePath">
		/// The new location for the file, with or without the filename attached.
		/// Without the filename, be sure to add a trailing slash (/) or make
		/// sure the directory exists, otherwise, the file may be renamed to
		/// what directory you wanted it in. To be sure, specify the full path
		/// for the file here.
		/// </param>
		void Move(string originalFilePath, string newFilePath);
	}
}
