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
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;

	public static class PathUtil
	{
		// can of worms shut!
		// TODO: This won't work on POSIX systems!
		private const string ROOTED = @"^
(?<root>
   
   (?<server>\\\\
    ([\w\-]{1,}
     |(?<ip4>(25[0-5]|2[0-4]\d|[0-1]?\d?\d)(\.(25[0-5]|2[0-4]\d|[0-1]?\d?\d)){3})

# thanks to http://blogs.msdn.com/mpoulson/archive/2005/01/10/350037.aspx
     |(?<ip6>(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}
       |(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4} # fully expanded
       |((?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4})*)?)::((?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4})*)?)
       |((?:[0-9A-Fa-f]{1,4}:){6,6})(25[0-5]|2[0-4]\d|[0-1]?\d?\d)(\.(25[0-5]|2[0-4]\d|[0-1]?\d?\d)){3}
       |((?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4})*)?) ::((?:[0-9A-Fa-f]{1,4}:)*)(25[0-5]|2[0-4]\d|[0-1]?\d?\d)(\.(25[0-5]|2[0-4]\d|[0-1]?\d?\d)){3}
# TODO: 2001:0db8::1428:57ab and 2001:0db8:0:0::1428:57ab are not matched!
     )
    )
    \\
   )
  |(?<qmarkpre>\\\\
    \?\\
    ((\k<qmarkpre>
     |UNC\\((\w+)|[A-Z]{1,3}:)
     |[A-Z]{1,3}:
    )\\)?
   )
  |(?<device>\\\\
     \.\\((?<devname>\w+)|(?<devguid>\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}))\\
   )
  |(?<windev>
    ((?<wdrive>[A-Z]{1,3}):)?
    \\
   )
)
(?<rest>(?<hdd>[A-Z]{1,3}:)?(?<nonrootpath>(?!\\)(.*)))? # folders here";

		private static Regex _Regex;
		private static int _RootIndex;
		private static int _NonRootIndex;
		private static List<string> _Reserved;
		private static List<char> _InvalidChars;
		private static int _DriveIndex;

		static PathUtil()
		{
			_Reserved = new List<string>("CON|PRN|AUX|NUL|COM1|COM2|COM3|COM4|COM5|COM6|COM7|COM8|COM9|LPT1|LPT2|LPT3|LPT4|LPT5|LPT6|LPT7|LPT8|LPT9"
			                             	.Split('|'));
			_InvalidChars = new List<char>(Path.GetInvalidPathChars());

			_Regex = new Regex(ROOTED,
			                   RegexOptions.Compiled |
			                   RegexOptions.IgnorePatternWhitespace |
			                   RegexOptions.IgnoreCase |
			                   RegexOptions.Multiline);

			_RootIndex = _Regex.GroupNumberFromName("root");
			_NonRootIndex = _Regex.GroupNumberFromName("nonrootpath");
			_DriveIndex = _Regex.GroupNumberFromName("wdrive");
		}

		public static bool IsRooted(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (path == string.Empty) return false;

			path = NormDirSepChars(path);

			var matches = _Regex.Matches(path);
			var matchC = matches.Count;

			for (int i = 0; i < matchC; i++)
			{
				if (matches[i].Groups[_RootIndex].Success)
					return true;
			}

			return false;
		}

		public static string GetPathRoot(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (path == string.Empty) throw new ArgumentException("path was empty.");
			if (containsInvalidChars(path)) throw new ArgumentException("path contains invalid characters.");

			path = NormDirSepChars(path);

			var matches = _Regex.Matches(path);
			var matchC = matches.Count;

			for (int i = 0; i < matchC; i++)
			{
				if (matches[i].Groups[_RootIndex].Success)
					return matches[i].Groups[_RootIndex].Value;
			}

			return string.Empty;
		}

		private static bool containsInvalidChars(string path)
		{
			int c = _InvalidChars.Count;
			int l = path.Length;

			for (int i = 0; i < l; i++)
				for (int j = 0; j < c; j++)
					if (path[i] == _InvalidChars[j])
						return true;
			return false;
		}

		public static string GetPathWithoutRoot(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (path.Length == 0) return string.Empty;
			path = NormDirSepChars(path);

			return path.Substring(GetPathRoot(path).Length);
		}

		///<summary>
		/// Normalize all the directory separation chars.
		/// Also removes empty space in beginning and end of string.
		///</summary>
		///<param name="pathWithAlternatingChars"></param>
		///<returns>The directory string path with all occurrances of the alternating chars
		/// replaced for that specified in <see cref="Path.DirectorySeparatorChar"/></returns>
		public static string NormDirSepChars(string pathWithAlternatingChars)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < pathWithAlternatingChars.Length; i++)
				if ((pathWithAlternatingChars[i] == '\\') || (pathWithAlternatingChars[i] == '/'))
					sb.Append(Path.DirectorySeparatorChar);
				else
					sb.Append(pathWithAlternatingChars[i]);
			return sb.ToString().Trim(new[] { ' ' });
		}

		public static string GetPathInfo(string path, out string nonrootpath, out string drive)
		{
			if (path == null) throw new ArgumentNullException("path");

			var matches = _Regex.Matches(path);
			var matchC = matches.Count;
			string root = drive = nonrootpath = string.Empty;

			for (int i = 0; i < matchC; i++)
			{
				if (matches[i].Groups[_RootIndex].Success)
					root = matches[i].Groups[_RootIndex].Value;
				if (matches[i].Groups[_NonRootIndex].Success)
					nonrootpath = matches[i].Groups[_NonRootIndex].Value;
				if (matches[i].Groups[_DriveIndex].Success)
					drive = matches[i].Groups[_DriveIndex].Value;
			}

			if (string.IsNullOrEmpty(drive))
			{
				drive = "C";
			}

			return root;
		}

		public static string GetFullPath(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (path.StartsWith("\\\\?\\") || path.StartsWith("\\\\.\\")) return Path.GetFullPath(path.Substring(4));
			if (path.StartsWith("\\\\?\\UNC\\")) return Path.GetFullPath(path.Substring(8));
			return Path.GetFullPath(path);
		}

		/// <summary>
		/// For a path "/a/b/c" would return "/a/b"
		/// or for "\\?\C:\folderA\folder\B\C\d.txt" would return "\\?\C:\folderA\folder\B\C"
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string GetPathWithoutLastBit(string path)
		{
			if (path == null) throw new ArgumentNullException("path");

			var chars = new List<char>(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

			bool endsWithSlash = false;
			int secondLast = -1;
			int last = -1;
			char lastType = chars[0];

			for (int i = 0; i < path.Length; i++)
			{
				if (i == path.Length - 1 && chars.Contains(path[i]))
					endsWithSlash = true;

				if (!chars.Contains(path[i])) continue;

				secondLast = last;
				last = i;
				lastType = path[i];
			}

			if (last == -1)
				throw new ArgumentException(string.Format("Could not find a path separator character in the path \"{0}\"", path));

			var res = path.Substring(0, endsWithSlash ? secondLast : last);
			return res == string.Empty ? new string(lastType, 1) : res;
		}
	}
}
