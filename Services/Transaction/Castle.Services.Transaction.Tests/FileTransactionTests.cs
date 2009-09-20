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

namespace Castle.Services.Transaction.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Transactions;
	using NUnit.Framework;

	[TestFixture]
	public class FileTransactionTests
	{
		private string dllPath;
		private string testFixturePath;
		private readonly List<string> filesCreated = new List<string>();

		[TestFixtureSetUp]
		public void Setup()
		{
			dllPath = Environment.CurrentDirectory;
			testFixturePath = dllPath.Combine("..\\..\\Kernel");
		}

		[TearDown]
		public void RemoveAllCreatedFiles()
		{
			foreach (var filePath in filesCreated)
			{
				if (File.Exists(filePath))
					File.Delete(filePath);
			}
		}

		[SetUp]
		public void CleanOutListEtc()
		{
			filesCreated.Clear();
		}

		[Test]
		public void CTorTests()
		{
			var t = new FileTransaction();
			Assert.AreEqual(t.Status, TransactionStatus.NoTransaction);
		}

		[Test]
		public void CreateFileTranscationally_Commit()
		{
			var filepath = testFixturePath.CombineAssert("temp").Combine("test");

			Assert.IsTrue(!File.Exists(filepath), "We must clean out the file after every test.");

			filesCreated.Add(filepath);

			using (var tx = new FileTransaction("Commit TX"))
			{
				tx.Begin();
				tx.WriteAllText(filepath, "Transactioned file.");
				tx.Commit();

				Assert.IsTrue(tx.Status == TransactionStatus.Committed);
			}

			Assert.IsTrue(File.Exists(filepath), "The file should exists after the transaction.");
			Assert.AreEqual(File.ReadAllLines(filepath)[0], "Transactioned file.");
		}

		[Test]
		public void CreateFileAndReplaceContents()
		{
			var filePath = testFixturePath.CombineAssert("temp")
				.Combine("temp__");
			filesCreated.Add(filePath);

			// simply write something to to file.
			using (var wr = File.CreateText(filePath))
				wr.WriteLine("Hello");

			using (var tx = new FileTransaction())
			{
				tx.Begin();

				using (var fs = (tx as IFileAdapter).Create(filePath))
				{
					var str = new UTF8Encoding().GetBytes("Goodbye");
					fs.Write(str, 0, str.Length);
					fs.Flush();
				}

				tx.Commit();
			}

			Assert.AreEqual(File.ReadAllLines(filePath)[0], "Goodbye");
		}

		[Test]
// ReSharper disable InconsistentNaming
		public void CreateFileTransactionally_Rollback()
// ReSharper restore InconsistentNaming
		{
			var filePath = testFixturePath.CombineAssert("temp")
				.Combine("temp2"); 
			filesCreated.Add(filePath);

			// simply write something to to file.
			using (var wr = File.CreateText(filePath))
				wr.WriteLine("Hello");

			Console.WriteLine("### " + filePath);

			Assert.IsTrue(File.Exists(filePath));
			
			using (var tx = new FileTransaction("File Transaction"))
			{
				tx.Begin();

				using (var fs = tx.Open(filePath, FileMode.Truncate))
				{
					var str = new UTF8Encoding().GetBytes("Goodbye");
					fs.Write(str, 0, str.Length);
					fs.Flush();
				}

				tx.Rollback();
			}

			Assert.AreEqual(File.ReadAllLines(filePath)[0], "Hello");
		}

		[Test]
		public void Using_TransactionScope_IsDistributed_AlsoTestingStatusWhenRolledBack()
		{
			using (new TransactionScope())
			{
				using (var tx = new FileTransaction())
				{
					tx.Begin();

					Assert.IsTrue(tx.DistributedTransaction);

					tx.Rollback();
					Assert.IsTrue(tx.IsRollbackOnlySet);
					Assert.AreEqual(tx.Status, TransactionStatus.RolledBack);
				}
			}
		}

		[Test]
		public void CannotCommitAfterSettingRollbackOnly()
		{
			using (var tx = new FileTransaction())
			{
				tx.Begin();
				tx.SetRollbackOnly();
				try
				{
					tx.Commit();
					Assert.Fail("Could commit when rollback only was set.");
				}
				catch (TransactionException)
				{
				}
			}
		}

		[Test]
		public void WhenCallingBeginTwice_WeSimplyReturn_Also_TestForRollbackedState()
		{
			using (var tx = new FileTransaction())
			{
				Assert.AreEqual(tx.Status, TransactionStatus.NoTransaction);
				tx.Begin();
				Assert.AreEqual(tx.Status, TransactionStatus.Active);
				Assert.IsFalse(tx.DistributedTransaction);
				tx.Begin();
				Assert.IsFalse(tx.DistributedTransaction, "Starting the same transaction twice should make no difference.");
				Assert.AreEqual(tx.Status, TransactionStatus.Active);
				tx.Commit();
				Assert.AreEqual(tx.Status, TransactionStatus.Committed);
			}
		}

		#region Resource

		private class R : IResource
		{
			public void Start()
			{
			}

			public void Commit()
			{
			}

			public void Rollback()
			{
				throw new ApplicationException("Expected.");
			}
		}

		#endregion

		[Test, ExpectedException(typeof (TransactionException))]
		public void InvalidStateOnCreate_Throws()
		{
			using (var tx = new FileTransaction())
			{
				(tx as IDirectoryAdapter).Create("lol");
			}
		}

		// Directories

		[Test]
		public void CanCreateAndFindDirectoryWithinTx()
		{
			using (var tx = new FileTransaction())
			{
				tx.Begin();
				Assert.IsFalse((tx as IDirectoryAdapter).Exists("something"));
				(tx as IDirectoryAdapter).Create("something");
				Assert.IsTrue((tx as IDirectoryAdapter).Exists("something"));
				tx.Rollback();
			}
		}

		[Test]
		public void CreatingFolder_InTransaction_AndCommitting_MeansExistsAfter()
		{
			var directoryPath = "testing";
			Assert.IsFalse(Directory.Exists(directoryPath));

			using (var tx = new FileTransaction())
			{
				tx.Begin();
				(tx as IDirectoryAdapter).Create(directoryPath);
				tx.Commit();
			}

			Assert.IsTrue(Directory.Exists(directoryPath));

			Directory.Delete(directoryPath);
		}

		[Test]
		public void NoCommit_MeansNoDirectory()
		{
			var directoryPath = "testing";
			Assert.IsFalse(Directory.Exists(directoryPath));

			using (var tx = new FileTransaction())
			{
				tx.Begin();
				(tx as IDirectoryAdapter).Create(directoryPath);
			}

			Assert.IsTrue(!Directory.Exists(directoryPath));
		}

		[Test]
		public void NonExistentDir()
		{
			using (var t = new FileTransaction())
			{
				t.Begin();
				var dir = (t as IDirectoryAdapter);
				Assert.IsFalse(dir.Exists("/hahaha"));
				Assert.IsFalse(dir.Exists("another_non_existent"));
				dir.Create("existing");
				Assert.IsTrue(dir.Exists("existing"));
			}
			// no commit
			Assert.IsFalse(Directory.Exists("existing"));
		}

		[Test]
		public void TestDeleteRecursive()
		{
			// 1. Create directory
			var pr = dllPath.Combine("testing");
			Directory.CreateDirectory(pr);
			Directory.CreateDirectory(pr.Combine("one"));
			Directory.CreateDirectory(pr.Combine("two"));
			Directory.CreateDirectory(pr.Combine("three"));

			File.WriteAllLines(pr.Combine("one").Combine("fileone"), new[] {"Hello world", "second line"});
			File.WriteAllLines(pr.Combine("one").Combine("filetwo"), new[] {"two", "second line"});
			File.WriteAllLines(pr.Combine("two").Combine("filethree"), new[] {"three", "second line"});

			using (var t = new FileTransaction())
			{
				t.Begin();
				Assert.IsTrue((t as IDirectoryAdapter).Delete(pr, true));
				t.Commit();
			}
		}

		[Test]
		public void CanMove_FileOrDirectory()
		{
			// TODO: implement using the for tx kernel...
		}
	}
}
