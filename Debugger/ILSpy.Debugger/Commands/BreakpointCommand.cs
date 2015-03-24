﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.AvalonEdit;
using ICSharpCode.ILSpy.Bookmarks;
using ICSharpCode.ILSpy.Debugger.Bookmarks;
using ICSharpCode.ILSpy.Debugger.Services;
using ICSharpCode.ILSpy.TextView;
using dnlib.DotNet;

namespace ICSharpCode.ILSpy.Debugger.Commands
{
	[ExportBookmarkActionEntry(Icon = "images/Breakpoint.png", Category="Debug")]
	public class BreakpointCommand : IBookmarkActionEntry
	{
		public bool IsEnabled()
		{
			return MainWindow.Instance.ActiveTextView != null;
		}
		
		public void Execute(int line)
		{
			var textView = MainWindow.Instance.ActiveTextView;
			if (textView != null)
				BreakpointHelper.Toggle(textView, line, 0);
		}
	}

	static class BreakpointHelper
	{
		public static bool IsEnabled(this IEnumerable<BreakpointBookmark> bps)
		{
			foreach (var bp in bps) {
				if (bp.IsEnabled)
					return true;
			}
			return false;
		}

		public static bool IsDisabled(this IEnumerable<BreakpointBookmark> bps)
		{
			return !bps.IsEnabled();
		}

		public static List<BreakpointBookmark> GetBreakpointBookmarks(DecompilerTextView textView, int line, int column)
		{
			return GetBreakpointBookmarks(Find(textView, line, column));
		}

		static List<BreakpointBookmark> GetBreakpointBookmarks(IList<SourceCodeMapping> mappings)
		{
			var list = new List<BreakpointBookmark>();
			if (mappings.Count == 0)
				return list;
			var mapping = mappings[0];
			foreach (var bm in BookmarkManager.Bookmarks) {
				var bpm = bm as BreakpointBookmark;
				if (bpm == null)
					continue;
				if (bpm.Location != mapping.StartLocation || bpm.EndLocation != mapping.EndLocation)
					continue;

				list.Add(bpm);
			}

			return list;
		}

		public static void Toggle(DecompilerTextView textView, int line, int column)
		{
			var bps = Find(textView, line, column);
			var bpms = GetBreakpointBookmarks(bps);
			if (bpms.Count > 0) {
				foreach (var bpm in bpms)
					BookmarkManager.RemoveMark(bpm);
			}
			else if (bps.Count > 0) {
				foreach (var bp in bps)
					BookmarkManager.AddMark(new BreakpointBookmark(bp.MemberMapping.MethodDefinition, bp.StartLocation, bp.EndLocation, bp.ILInstructionOffset));
				textView.ScrollAndMoveCaretTo(bps[0].StartLocation.Line, bps[0].StartLocation.Column);
			}
		}

		public static IList<SourceCodeMapping> Find(DecompilerTextView textView, int line, int column)
		{
			if (textView == null)
				return null;
			return Find(textView.CodeMappings, line, column);
		}

		public static IList<SourceCodeMapping> Find(Dictionary<MethodKey, MemberMapping> cm, int line, int column)
		{
			if (line <= 0)
				return new SourceCodeMapping[0];
			if (cm == null || cm.Count == 0)
				return new SourceCodeMapping[0];

			var bp = FindByLineColumn(cm, line, column);
			if (bp == null && column != 0)
				bp = FindByLineColumn(cm, line, 0);
			if (bp == null)
				bp = GetClosest(cm, line);

			if (bp != null)
				return bp;
			return new SourceCodeMapping[0];
		}

		static List<SourceCodeMapping> FindByLineColumn(Dictionary<MethodKey, MemberMapping> cm, int line, int column)
		{
			List<SourceCodeMapping> list = null;
			foreach (var storageEntry in cm.Values) {
				var bp = storageEntry.GetInstructionByLineNumber(line, column);
				if (bp != null) {
					if (list == null)
						list = new List<SourceCodeMapping>();
					list.Add(bp);
				}
			}
			return list;
		}

		static List<SourceCodeMapping> GetClosest(Dictionary<MethodKey, MemberMapping> cm, int line)
		{
			List<SourceCodeMapping> list = new List<SourceCodeMapping>();
			foreach (var entry in cm.Values) {
				SourceCodeMapping map = null;
				foreach (var m in entry.MemberCodeMappings) {
					if (line > m.EndLocation.Line)
						continue;
					if (map == null || m.StartLocation < map.StartLocation)
						map = m;
				}
				if (map != null) {
					if (list.Count == 0)
						list.Add(map);
					else if (map.StartLocation == list[0].StartLocation)
						list.Add(map);
					else if (map.StartLocation < list[0].StartLocation) {
						list.Clear();
						list.Add(map);
					}
				}
			}

			if (list.Count == 0)
				return null;
			return list;
		}
	}

	[ExportBookmarkContextMenuEntry(InputGestureText = "Shift+F9",
									Category = "Debug")]
	public class EnableAndDisableBreakpointCommand : IBookmarkContextMenuEntry2
	{
		public bool IsVisible(IBookmark bookmark)
		{
			return bookmark is BreakpointBookmark;
		}

		public bool IsEnabled(IBookmark bookmark)
		{
			return IsVisible(bookmark);
		}

		public void Execute(IBookmark bookmark)
		{
			var bpm = bookmark as BreakpointBookmark;
			if (bpm != null)
				bpm.IsEnabled = !bpm.IsEnabled;
		}

		public void Initialize(IBookmark bookmark, MenuItem menuItem)
		{
			var bpm = bookmark as BreakpointBookmark;
			if (bpm != null)
				InitializeMenuItem(new[] { bpm }, menuItem);
		}

		public static void InitializeMenuItem(IList<BreakpointBookmark> bpms, MenuItem menuItem)
		{
			menuItem.IsEnabled = bpms.Count > 0;
			if (bpms.IsEnabled()) {
				menuItem.Header = bpms.Count <= 1 ? "Disable _Breakpoint" : "Disable _Breakpoints";
				menuItem.Icon = ImageService.LoadImage(ImageService.DisabledBreakpoint, 16, 16);
			}
			else {
				menuItem.Header = bpms.Count <= 1 ? "Enable _Breakpoint" : "Enable _Breakpoints";
				menuItem.Icon = ImageService.LoadImage(ImageService.Breakpoint, 16, 16);
			}
		}
	}
}
