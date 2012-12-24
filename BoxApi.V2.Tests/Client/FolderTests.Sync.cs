using System;
using System.Linq;
using BoxApi.V2.Model;
using BoxApi.V2.Model.Enum;
using BoxApi.V2.Tests.Harness;
using NUnit.Framework;

namespace BoxApi.V2.Tests.Client
{
    [TestFixture]
    public class FolderTestsSync : BoxApiTestHarness
    {
        [Test]
        public void GetFolder()
        {
            var folder = Client.GetFolder(RootId);
            AssertFolderConstraints(folder, "All Files", null, RootId);
        }

        [Test]
        public void GetFolderItems()
        {
            var testFolder = Client.CreateFolder(RootId, TestItemName());
            Client.CreateFolder(testFolder.Id, TestItemName());
            Client.CreateFolder(testFolder.Id, TestItemName());

            try
            {
                var items = Client.GetItems(testFolder, new[] {Field.CreatedAt, Field.Name,});
                Assert.That(items, Is.Not.Null);
                Assert.That(items.TotalCount, Is.EqualTo(2));
                // expected present
                Assert.That(items.Entries.All(e => e.Id != null));
                Assert.That(items.Entries.All(e => e.Name != null));
                Assert.That(items.Entries.All(e => e.CreatedAt != null));
                // expected empty
                Assert.That(items.Entries.All(e => e.CreatedBy == null));
                Assert.That(items.Entries.All(e => e.OwnedBy == null));
            }
            finally
            {
                Client.Delete(testFolder, true);
            }
        }

        [Test]
        public void FieldsWorksOnGetFolder()
        {
            var fileName = TestItemName();
            var testFile = Client.CreateFile(RootId, fileName, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            try
            {
                var folder = Client.GetFolder(RootId, new[] {Field.Name, Field.Size, Field.Etag,});
                var actual = folder.Files.Single(f => f.Id.Equals(testFile.Id));
                // expect present
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual.Name, Is.EqualTo(testFile.Name));
                Assert.That(actual.Size, Is.EqualTo(testFile.Size));
                Assert.That(actual.Etag, Is.EqualTo(testFile.Etag));
                // expect empty
                Assert.That(actual.CreatedBy, Is.Null);
                Assert.That(actual.OwnedBy, Is.Null);
            }
            finally
            {
                Client.Delete(testFile);
            }
        }

        [Test]
        public void CanGetSubfolderFromFoldersProperty()
        {
            var folderName = TestItemName();
            var testFolder = Client.CreateFolder(Folder.Root, folderName);

            try
            {
                var folder = Client.GetFolder(RootId, new[] { Field.Name, Field.Size, Field.Etag, });
                var actual = folder.Folders.Single(f => f.Id.Equals(testFolder.Id));
                // expect present
                Assert.That(actual, Is.Not.Null);
                // expect empty
            }
            finally
            {
                Client.Delete(testFolder);
            }
            
        }


        [Test, ExpectedException(typeof (BoxException))]
        public void GetNonExistentFolder()
        {
            Client.GetFolder("abc");
        }

        [Test]
        public void CreateFolder()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName);
            AssertFolderConstraints(folder, folderName, RootId);
            // clean up 
            Client.Delete(folder, true);
        }

        [Test, ExpectedException(typeof(BoxItemNotModifiedException))]
        public void SubsequentGetThrowsNotModifiedException()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName);
            try
            {
                Client.Get(folder, null, folder.Etag);
            }
            finally
            {
                // clean up 
                Client.Delete(folder, true);
            }
        }

        [Test]
        public void FieldsWorkOnCreateFolder()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, new[]{Field.Name, Field.Parent, });
            AssertFolderConstraints(folder, folderName, RootId);
            Assert.That(folder.CreatedAt, Is.Null);
            Assert.That(folder.CreatedBy, Is.Null);
            // clean up 
            Client.Delete(folder, true);
        }

        [Test, ExpectedException(typeof (BoxException))]
        public void CreateFolderWithIllegalName()
        {
            Client.CreateFolder(RootId, "\\bad name:");
        }

        [Test]
        public void DeleteFolder()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName);
            Client.Delete(folder, true);
        }

        [Test, ExpectedException(typeof(BoxItemModifiedException))]
        public void DeleteFolderFailsIfEtagIsStale()
        {
            var folderName = TestItemName();
            var original = Client.CreateFolder(RootId, folderName);
            var current = Client.UpdateDescription(original, "new description");
            try
            {
                Client.Delete(original, true, original.Etag);
                Assert.Fail();
            }
            finally
            {
                Client.Delete(current, true);
            }
        }


        [Test]
        public void DeleteFolderRecursive()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var subFolder = Client.CreateFolder(folder.Id, "subfolder", null);
            Client.Delete(folder, true);
        }

        [Test, ExpectedException(typeof (BoxException))]
        public void DeleteNonEmptyFolderWithoutRecursiveBitSet()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var subFolder = Client.CreateFolder(folder.Id, "subfolder", null);
            try
            {
                // Should barf.
                Client.Delete(folder, false);
            }
            finally
            {
                // clean up.
                Client.Delete(folder, true);
            }
        }

        [Test, ExpectedException(typeof (BoxException))]
        public void DeleteNonExistentFolder()
        {
            Client.DeleteFolder("1234123", true);
        }

        [Test]
        public void CopyFolderToSameParentWithDifferentName()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var copyName = TestItemName();
            var copy = Client.Copy(folder, RootId, copyName, null);
            AssertFolderConstraints(copy, copyName, RootId);
            Assert.That(copy.Parent.Id, Is.EqualTo(RootId));
            Client.Delete(folder, true);
            Client.Delete(copy, true);
        }

        [Test]
        public void CopyFolderToDifferentParentWithSameName()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var destinationName = TestItemName();
            var destination = Client.CreateFolder(RootId, destinationName, null);

            var copy = Client.Copy(folder, destination, null, null);
            AssertFolderConstraints(copy, folderName, destination.Id);
            Assert.That(copy.Parent.Id, Is.EqualTo(destination.Id));
            Client.Delete(folder, true);
            Client.Delete(destination, true);
        }

        [Test]
        public void CopyRecursive()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var subfolder = Client.CreateFolder(folder.Id, "subfolder", null);
            var copyName = TestItemName();
            var copy = Client.Copy(folder, RootId, copyName, null);
            Assert.That(copy.ItemCollection.TotalCount, Is.EqualTo(1));
            Client.Delete(folder, true);
            Client.Delete(copy, true);
        }

        [Test, ExpectedException(typeof (BoxException))]
        public void CopyFolderFailsWhenSameParentAndNewNameNotProvided()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            try
            {
                Client.Copy(folder, RootId, null, null);
            }
            finally
            {
                Client.Delete(folder, true);
            }
        }

        [Test, ExpectedException(typeof (BoxException))]
        public void CopyFolderFailsWhenSameParentAndNewNameIsSame()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            try
            {
                Client.Copy(folder, RootId, folder.Name, null);
            }
            finally
            {
                Client.Delete(folder, true);
            }
        }

        [Test]
        public void CreateSharedLink()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var sharedLink = new SharedLink(Access.Open, DateTime.UtcNow.AddDays(3), new Permissions {CanPreview = true, CanDownload = true});
            var update = Client.ShareLink(folder, sharedLink);
            AssertFolderConstraints(update, folderName, RootId, folder.Id);
            AssertSharedLink(update.SharedLink, sharedLink);
            Client.Delete(update, true);
        }

        [Test]
        public void MoveFolder()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var targetFolderName = TestItemName();
            var targetFolder = Client.CreateFolder(RootId, targetFolderName, null);
            var moved = Client.Move(folder, targetFolder);
            AssertFolderConstraints(moved, folderName, targetFolder.Id, folder.Id);
            Client.Delete(targetFolder, true);
        }

        [Test, ExpectedException(typeof (BoxException)),
         Description("This fails, but eventually won't.  See http://stackoverflow.com/questions/12439723/moving-folder-to-same-parent-returns-400-bad-request")]
        public void MoveFolderToSameParent()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            try
            {
                var moved = Client.Move(folder, RootId);
                AssertFolderConstraints(moved, folderName, RootId, folder.Id);
            }
            finally
            {
                Client.DeleteFolder(folder.Id, true);
            }
        }

        [Test]
        public void RenameFolder()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var newName = TestItemName();
            var moved = Client.Rename(folder, newName);
            AssertFolderConstraints(moved, newName, RootId, folder.Id);
            Client.DeleteFolder(folder.Id, true);
        }

        [Test]
        public void RenameFolderToSameName()
        {
            var folderName = TestItemName();
            var folder = Client.CreateFolder(RootId, folderName, null);
            var moved = Client.Rename(folder, folderName);
            AssertFolderConstraints(moved, folderName, RootId, folder.Id);
            Client.Delete(folder, true);
        }

        [Test]
        public void UpdateDescription()
        {
            var folderName = TestItemName();
            var newDescription = "new description";
            var folder = Client.CreateFolder(RootId, folderName, null);
            // Act
            try
            {
                var updatedFolder = Client.UpdateDescription(folder, newDescription);
                // Assert
                AssertFolderConstraints(updatedFolder, folderName, RootId, folder.Id);
                Assert.That(updatedFolder.Description, Is.EqualTo(newDescription));
            }
            finally
            {
                Client.Delete(folder);
            }
        }

        [Test]
        public void UpdateEverything()
        {
            var folder = Client.CreateFolder(RootId, TestItemName(), null);
            var newDescription = "new description";
            var newParent = Client.CreateFolder(RootId, TestItemName(), null);
            var sharedLink = new SharedLink(Access.Open, DateTime.UtcNow.AddDays(3), new Permissions {CanDownload = true, CanPreview = true});
            var newName = TestItemName();
            // Act
            try
            {
                folder.Parent.Id = newParent.Id;
                folder.Description = newDescription;
                folder.Name = newName;
                folder.SharedLink = sharedLink;
                var updatedFolder = Client.Update(folder);
                // Assert
                AssertFolderConstraints(updatedFolder, newName, newParent.Id, folder.Id);
                AssertSharedLink(sharedLink, folder.SharedLink);
                Assert.That(updatedFolder.Description, Is.EqualTo(newDescription));
            }
            finally
            {
                Client.Delete(newParent, true);
            }
        }
    }
}