﻿using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Authentication;
using FluentFTP;
using FluentFTP.Servers;
#if (CORE || NETFX)
using System.Threading;
using FluentFTP.Helpers;
using System.Linq;
#endif
#if ASYNC
using System.Threading.Tasks;
#endif

namespace FluentFTP.Servers.Handlers {

	/// <summary>
	/// Server-specific handling for IBMzOSFTP servers
	/// </summary>
	internal class IBMzOSFtpServer : FtpBaseServer {

		/// <summary>
		/// Return the FtpServer enum value corresponding to your server, or Unknown if its a custom implementation.
		/// </summary>
		public override FtpServer ToEnum() {
			return FtpServer.IBMzOSFTP;
		}

		/// <summary>
		/// Return true if your server is detected by the given FTP server welcome message.
		/// </summary>
		public override bool DetectByWelcome(string message) {

			// Detect IBM z/OS server
			// Welcome message: "220-FTPD1 IBM FTP CS V2R3 at mysite.gov, 16:51:54 on 2019-12-12."
			if (message.Contains("IBM FTP CS")) {
				return true;
			}

			return false;
		}

		/// <summary>
		/// Return the default file listing parser to be used with your FTP server.
		/// </summary>
		public override FtpParser GetParser() {
			return FtpParser.IBMzOS;
		}

		/// <summary>
		/// Perform server-specific post-connection commands here.
		/// Return true if you executed a server-specific command.
		/// </summary>
		public override void AfterConnected(FtpClient client) {
			FtpReply reply;
			if (!(reply = client.Execute("SITE DATASETMODE")).Success) {
				throw new FtpCommandException(reply);
			}
			if (!(reply = client.Execute("SITE QUOTESOVERRIDE")).Success) {
				throw new FtpCommandException(reply);
			}
		}

#if ASYNC
		/// <summary>
		/// Perform server-specific post-connection commands here.
		/// Return true if you executed a server-specific command.
		/// </summary>
		public override async Task AfterConnectedAsync(FtpClient client, CancellationToken token) {
			FtpReply reply;
			if (!(reply = await client.ExecuteAsync("SITE DATASETMODE", token)).Success) {
				throw new FtpCommandException(reply);
			}
			if (!(reply = await client.ExecuteAsync("SITE QUOTESOVERRIDE", token)).Success) {
				throw new FtpCommandException(reply);
			}
		}
#endif


		public override bool IsCustomFileSize() {
			return true;
		}

		/// <summary>
		/// Get z/OS file size
		/// </summary>
		/// <param name="path">The full path of the file whose size you want to retrieve</param>
		/// <remarks>
		/// Make sure you are in the right realm (z/OS or HFS) before doing this
		/// </remarks>
		/// <returns>The size of the file</returns>
		public override long GetFileSize(FtpClient client, string path) {

			// get metadata of the file
			FtpListItem[] entries = client.GetListing(path);

			// no entries or more than one: path is NOT for a single dataset or file
			if (entries.Length != 1) { return -1; }

			// if the path is for a SINGLE dataset or file, there will be only one entry
			FtpListItem entry = entries[0];

			// z/OS list parser will have determined that size
			return entry.Size;
		}

#if ASYNC
		/// <summary>
		/// Get z/OS file size
		/// </summary>
		/// <param name="path">The full path of the file whose size you want to retrieve</param>
		/// <remarks>
		/// Make sure you are in the right realm (z/OS or HFS) before doing this
		/// </remarks>
		/// <returns>The size of the file</returns>
		public override async Task<long> GetFileSizeAsync(FtpClient client, string path, CancellationToken token) {

			// prevent automatic parser detection switching to unix on HFS paths
			client.ListingParser = FtpParser.IBMzOS;

			// get metadata of the file
			FtpListItem[] entries = await client.GetListingAsync(path, token);

			// no entries or more than one: path is NOT for a single dataset or file
			if (entries.Length != 1) return -1;

			// if the path is for a SINGLE dataset or file, there will be only one entry
			FtpListItem entry = entries[0];

			// z/OS list parser will have determined that size
			return entry.Size;
		}
#endif

		/// <summary>
		/// Check if the given path is a root directory on your FTP server.
		/// If you are unsure, return false.
		/// </summary>
		public override bool IsRoot(FtpClient client, string path) {

			// Note: If on z/OS you have somehow managed to CWD "over" the top, i.e.
			// PWD returns "''", it is also root - you would need to CWD to some HLQ
			// that only you can imagine. There is no way to list the available top
			// level HLQs.

			// z/OS HFS root
			if (path == "/") { 
				return true;
			}

			// z/OS HFS some path
			if (path.StartsWith("/")) {
				return false;
			}

			// z/OS HLQ (like 'SYS1.' or '')
			if (path.Trim('\'').TrimEnd('.').Split('.').Length <= 1) { 
				return true;
			}

			// all others
			return false;
		}

		/// <summary>
		/// Skip reporting a parser error
		/// </summary>
		public override bool SkipParserErrorReport() {
			return true;
		}

		/// <summary>
		/// Always read to End of stream on a download
		/// </summary>
		public override bool AlwaysReadToEnd(string remotePath)	{
			return true;
		}

		/// <summary>
		/// Return true if your server requires custom handling of absolute path.
		/// </summary>
		public override bool IsCustomGetAbsolutePath() {
			return true;
		}

		/// <summary>
		/// Perform server-specific path modification here.
		/// Return the absolute path.
		/// </summary>
		public override string GetAbsolutePath(FtpClient client, string path) {

			if (path == null || path.Trim().Length == 0) {
				path = client.GetWorkingDirectory();
				return path;
			}

			if (path.StartsWith("/") || path.StartsWith("\'")) {
				return path;
			}

			var pwd = client.GetWorkingDirectory();

			if (pwd.StartsWith("/")) {
				if (pwd.Equals("/")) {
					path = (pwd + path).GetFtpPath();
				}
				else {
					path = (pwd + "/" + path).GetFtpPath();
				}
				return path;
			}

			if (pwd.StartsWith("\'")) {
				if (pwd.Equals("`\'\'")) {
					path = ("\'" + path + "\'").GetFtpPath();
				}
				else {
					pwd = pwd.TrimEnd('\'');
					if (pwd.EndsWith(".")) {
						path = (pwd + path + "\'").GetFtpPath();
					}
					else {
						path = (pwd + "(" + path + ")\'").GetFtpPath();
					}
				}
				return path;
			}

			return path;
		}

#if ASYNC
		/// <summary>
		/// Perform server-specific path modification here.
		/// Return the absolute path.
		/// </summary>
		public override async Task<string> GetAbsolutePathAsync(FtpClient client, string path, CancellationToken token)	{

			if (path == null || path.Trim().Length == 0) {
				path = await client.GetWorkingDirectoryAsync(token);
				return path;
			}

			if (path.StartsWith("/") || path.StartsWith("\'")) {
				return path;
			}

			var pwd = await client.GetWorkingDirectoryAsync(token);

			if (pwd.StartsWith("/")) {
				if (pwd.Equals("/")) {
					path = (pwd + path).GetFtpPath();
				}
				else {
					path = (pwd + "/" + path).GetFtpPath();
				}
				return path;
			}

			if (pwd.StartsWith("\'")) {
				if (pwd.Equals("`\'\'")) {
					path = ("\'" + path + "\'").GetFtpPath();
				}
				else {
					pwd = pwd.TrimEnd('\'');
					if (pwd.EndsWith(".")) {
						path = (pwd + path + "\'").GetFtpPath();
					}
					else {
						path = (pwd + "(" + path + ")\'").GetFtpPath();
					}
				}
				return path;
			}

			return path;
		}
#endif

		/// <summary>
		/// Return true if your server requires custom handling of absolute dir.
		/// </summary>
		public override bool IsCustomGetAbsoluteDir() {
			return true;
		}

		/// <summary>
		/// Perform server-specific path modification here.
		/// Return null indicates custom code decided not to handle this
		/// Return the absolute dir.
		/// </summary>
		public override string GetAbsoluteDir(FtpClient client, string path) {
			path = client.ServerHandler.GetAbsolutePath(client, path);

			if (!path.StartsWith("\'")) {
				return null;
			}

			if (!path.EndsWith(".\'")) {
				path = path.TrimEnd('\'') + "(*)\'";
			}

			return path;
		}

#if ASYNC
		/// <summary>
		/// Perform server-specific path modification here.
		/// Return null indicates custom code decided not to handle this
		/// Return the absolute dir.
		/// </summary>
		public override async Task<string> GetAbsoluteDirAsync(FtpClient client, string path, CancellationToken token) {

			path = await client.ServerHandler.GetAbsolutePathAsync(client, path, token);

			if (!path.StartsWith("\'")) {
				return null;
			}

			if (!path.EndsWith(".\'")) {
				path = path.TrimEnd('\'') + "(*)\'";
			}

			return path;
		}
#endif

		/// <summary>
		/// Return true if your server requires custom handling of path and filename concatenation.
		/// </summary>
		public override bool IsCustomGetAbsoluteFilePath() {
			return true;
		}

		/// <summary>
		/// Perform server-specific path modification here.
		/// Return null indicates custom code decided not to handle this
		/// Return concatenation of path and filename
		/// </summary>
		public override string GetAbsoluteFilePath(FtpClient client, string path, string fileName) {

			if (!path.StartsWith("\'"))	{
				return null;
			}

			if (path.EndsWith(".\'")) {
				path = path.TrimEnd('\'') + "." + fileName + "\'";
			}
			else {
				path = path.TrimEnd('\'') + "(" + fileName + ")\'";
			}

			return path;
		}

#if ASYNC
		/// <summary>
		/// Perform server-specific path modification here.
		/// Return null indicates custom code decided not to handle this
		/// Return concatenation of path and filename
		/// </summary>
		public override async Task<string> GetAbsoluteFilePathAsync(FtpClient client, string path, string fileName, CancellationToken token) {

			if (!path.StartsWith("\'"))	{
				return null;
			}

			if (path.EndsWith(".\'")) {
				path = path.TrimEnd('\'') + "." + fileName + "\'";
			}
			else {
				path = path.TrimEnd('\'') + "(" + fileName.Substring(0, 8) + ")\'";
			}

			return path;
		}
#endif

		/// <summary>
		/// Return true if your server requires custom handling to handle listing analysis.
		/// </summary>
		public override bool IsCustomCalculateFullFtpPath() {
			return true;
		}

		/// <summary>
		/// Get the full path of a given FTP Listing entry
		/// Return null indicates custom code decided not to handle this
		/// </summary>
		public override bool? CalculateFullFtpPath(FtpClient client, string path, FtpListItem item) {
			if (client.zOSListingRealm == FtpZOSListRealm.Unix)	{
				return null;
			}

			// The user might be using GetListing("", FtpListOption.NoPath)
			// or he might be using    GetListing("not_fully_qualified_zOS path")
			// or he might be using    GetListing("'fully_qualified_zOS path'") (note the single quotes)

			// The following examples in the comments assume a current working
			// directory of 'GEEK.'.

			// If it is not a FtpZOSListRealm.Dataset, it must be FtpZOSListRealm.Member*

			// Is caller using FtpListOption.NoPath and CWD to the right place?
			if (path.Length == 0) {
				if (client.zOSListingRealm == FtpZOSListRealm.Dataset)
				{
					// Path: ""
					// Fullname: 'GEEK.PROJECTS.LOADLIB'
					item.FullName = client.GetWorkingDirectory().TrimEnd('\'') + item.Name + "\'";
				}
				else {
					// Path: ""
					// Fullname: 'GEEK.PROJECTS.LOADLIB(MYPROG)'
					item.FullName = client.GetWorkingDirectory().TrimEnd('\'') + "(" + item.Name + ")\'";
				}
			}
			// Caller is not using FtpListOption.NoPath, so the fullname can be built
			// depending on the listing realm
			else if (path[0] == '\'') {
				if (client.zOSListingRealm == FtpZOSListRealm.Dataset) {
					// Path: "'GEEK.PROJECTS.LOADLIB'"
					// Fullname: 'GEEK.PROJECTS.LOADLIB'
					item.FullName = item.Name;
				}
				else {
					if (path.EndsWith("(*)\'"))	{
						// Path: "'GEEK.PROJECTS.LOADLIB(*)'"
						// Fullname: 'GEEK.PROJECTS.LOADLIB(MYPROG)'
						item.FullName = path.Substring(0, path.Length - 4) + "(" + item.Name + ")\'";
					}
					else {
						item.FullName = path;
					}
				}
			}
			else {
				if (client.zOSListingRealm == FtpZOSListRealm.Dataset) {
					// Path: "PROJECTS.LOADLIB"
					// Fullname: 'GEEK.PROJECTS.LOADLIB'
					item.FullName = client.GetWorkingDirectory().TrimEnd('\'') + item.Name + '\'';
				}
				else {
					if (path.EndsWith("(*)")) {
						// Path: "PROJECTS.LOADLIB(*)"
						// Fullname: 'GEEK.PROJECTS.LOADLIB(MYPROG)'
						item.FullName = client.GetWorkingDirectory().TrimEnd('\'') + path.Substring(0, path.Length - 3) + "(" + item.Name + ")\'";
					}
					else {
						item.FullName = path;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Disable SIZE command even if server says it is supported
		/// </summary>
		public override bool DontUseSizeEvenIfCapable(string path) {
			return !path.StartsWith("/");
		}

		/// <summary>
		/// Disable MDTM command even if server says it is supported
		/// </summary>
		public override bool DontUseMdtmEvenIfCapable(string path) {
			return !path.StartsWith("/");
		}

		/// <summary>
		/// Return true if your server requires custom handling to check file existence.
		/// </summary>
		public override bool IsCustomFileExists() {
			return true;
		}

		/// <summary>
		/// Check for existence of a file
		/// Return null indicates custom code decided not to handle this
		/// </summary>
		public override bool? FileExists(FtpClient client, string path)
		{
			if (path.StartsWith("/")) {
				return null;
			}

			var fileList = client.GetNameListing(path);
			return fileList.Count() > 0;
		}

#if ASYNC
		/// <summary>
		/// Check for existence of a file
		/// Return null indicates custom code decided not to handle this
		/// </summary>
		public override async Task<bool?> FileExistsAsync(FtpClient client, string path, CancellationToken token)
		{
			if (path.StartsWith("/")) {
				return null;
			}

			var fileList = await client.GetNameListingAsync(path, token);
			return fileList.Count() > 0;
		}
#endif
	}
}

