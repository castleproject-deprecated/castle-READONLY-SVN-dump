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
	///<summary>
	/// Directory helper.
	///</summary>
	public interface IDirectoryAdapter
	{
		/// <summary>Creates a directory at the path given.</summary>
		///<param name="path">The path to create the directory at.</param>
		void Create(string path);

		/// <summary>
		/// Checks whether the path exists.
		/// </summary>
		/// <param name="path">Path to check.</param>
		/// <returns>True if it exists, false otherwise.</returns>
		bool Exists(string path);

		/// <summary>
		/// Deletes a folder recursively.
		/// </summary>
		/// <param name="path"></param>
		void Delete(string path);

		/// <summary>
		/// Deletes a folder.
		/// </summary>
		/// <param name="path">The path to the folder to delete.</param>
		/// <param name="recursively">
		/// Whether to delete recursively or not.
		/// When recursive, we delete all subfolders and files in the given
		/// directory as well.
		/// </param>
		/// <returns>Whether the delete was successful.</returns>
		bool Delete(string path, bool recursively);

		/// <summary>
		/// Gets the full path of the specified directory.
		/// </summary>
		/// <param name="dir">The relative path.</param>
		/// <returns>A string with the full path.</returns>
		string GetFullPath(string dir);

		///<summary>
		/// Gets the MapPath of the path. 
		/// 
		/// This will be relative to the root web directory if we're in a 
		/// web site and otherwise to the executing assembly.
		///</summary>
		///<param name="path"></param>
		///<returns></returns>
		string MapPath(string path);

		///<summary>
		/// Moves the directory from the original path to the new path.
		///</summary>
		///<param name="originalPath">Path from</param>
		///<param name="newPath">Path to</param>
		void Move(string originalPath, string newPath);
	}
}
