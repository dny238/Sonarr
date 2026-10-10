using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private string _rootFolder;
        private string _releaseFolder;
        private string _videoFile;

        [SetUp]
        public void Setup()
        {
            _rootFolder = @"C:\Test\Downloads".AsOsAgnostic();
            _releaseFolder = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr".AsOsAgnostic();
            _videoFile = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\sample.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(Array.Empty<string>());
        }

        private void GivenTrackedDownload(string downloadId, string outputPath)
        {
            var trackedDownload = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem { DownloadId = downloadId },
                ImportItem = new DownloadClientItem { DownloadId = downloadId, OutputPath = new OsPath(outputPath) }
            };

            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.Find(downloadId))
                  .Returns(trackedDownload);
        }

        [Test]
        public void should_delete_file_in_folder()
        {
            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, false);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(_videoFile), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_not_delete_anything_if_a_file_is_outside_folder()
        {
            var outsideFile = @"C:\Test\TV\Series Title\Season 1\episode.mkv".AsOsAgnostic();

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile, outsideFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_delete_file_that_escapes_folder_using_relative_path()
        {
            var escapingFile = @"C:\Test\Downloads\..\TV\episode.mkv".AsOsAgnostic();

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(_rootFolder, null, new List<string> { escapingFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_delete_non_video_file()
        {
            var nonVideoFile = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\release.nfo".AsOsAgnostic();

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(_rootFolder, null, new List<string> { nonVideoFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_throw_if_folder_and_download_id_are_not_provided()
        {
            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(null, null, new List<string> { _videoFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_skip_file_that_no_longer_exists()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(_videoFile))
                  .Returns(false);

            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_delete_release_folder_when_no_video_files_remain()
        {
            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(_releaseFolder, true), Times.Once());
        }

        [Test]
        public void should_delete_top_level_release_folder_when_file_is_in_a_subfolder()
        {
            var nestedFile = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\Sample\sample.mkv".AsOsAgnostic();

            Subject.DeleteFiles(_rootFolder, null, new List<string> { nestedFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(_releaseFolder, true), Times.Once());
        }

        [Test]
        public void should_not_delete_release_folder_when_video_files_remain()
        {
            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.GetVideoFiles(_releaseFolder, It.IsAny<bool>()))
                  .Returns(new[] { @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\episode.mkv".AsOsAgnostic() });

            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(_videoFile), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.RemoveEmptySubfolders(_releaseFolder), Times.Once());
        }

        [Test]
        public void should_not_delete_release_folder_when_large_rar_files_remain()
        {
            var rarFile = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\release.rar".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(_releaseFolder, true))
                  .Returns(new[] { rarFile });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(rarFile))
                  .Returns(50.Megabytes());

            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(_videoFile), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_delete_release_folder_when_only_small_rar_files_remain()
        {
            var rarFile = @"C:\Test\Downloads\Series.Title.S01E01.720p.HDTV-Sonarr\subs.rar".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(_releaseFolder, true))
                  .Returns(new[] { rarFile });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(rarFile))
                  .Returns(1.Megabytes());

            Subject.DeleteFiles(_rootFolder, null, new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(_releaseFolder, true), Times.Once());
        }

        [Test]
        public void should_never_delete_the_folder_being_browsed()
        {
            var fileInRoot = @"C:\Test\Downloads\sample.mkv".AsOsAgnostic();

            Subject.DeleteFiles(_rootFolder, null, new List<string> { fileInRoot }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(fileInRoot), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_use_download_output_path_and_delete_it_when_no_video_files_remain()
        {
            GivenTrackedDownload("sab1", _releaseFolder);

            Subject.DeleteFiles(null, "sab1", new List<string> { _videoFile }, true);

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(_videoFile), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFolder(_releaseFolder, true), Times.Once());
        }

        [Test]
        public void should_not_delete_file_outside_download_output_path()
        {
            GivenTrackedDownload("sab1", _releaseFolder);

            var otherFile = @"C:\Test\Downloads\Other.Release\episode.mkv".AsOsAgnostic();

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(_rootFolder, "sab1", new List<string> { otherFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_throw_if_download_is_not_found()
        {
            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteFiles(null, "unknown", new List<string> { _videoFile }, false));

            Mocker.GetMock<IDiskProvider>().Verify(v => v.DeleteFile(It.IsAny<string>()), Times.Never());
        }
    }
}
