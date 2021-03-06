/*
    Copyright (C) 2014-2019 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using dnlib.DotNet;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Debugger.Breakpoints.Code;
using dnSpy.Contracts.Debugger.Breakpoints.Code.Dialogs;
using dnSpy.Contracts.Debugger.DotNet.Code;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.Metadata;
using dnSpy.Contracts.TreeView;

namespace dnSpy.Debugger.DotNet.Breakpoints.Code.TextEditor {
	[Export(typeof(MethodBreakpointsService))]
	sealed class MethodBreakpointsService {
		readonly Lazy<DbgManager> dbgManager;
		readonly Lazy<IModuleIdProvider> moduleIdProvider;
		readonly Lazy<DbgCodeBreakpointsService> dbgCodeBreakpointsService;
		readonly Lazy<DbgDotNetCodeLocationFactory> dbgDotNetCodeLocationFactory;
		readonly Lazy<ShowCodeBreakpointSettingsService> showCodeBreakpointSettingsService;

		[ImportingConstructor]
		MethodBreakpointsService(Lazy<DbgManager> dbgManager, Lazy<IModuleIdProvider> moduleIdProvider, Lazy<DbgCodeBreakpointsService> dbgCodeBreakpointsService, Lazy<DbgDotNetCodeLocationFactory> dbgDotNetCodeLocationFactory, Lazy<ShowCodeBreakpointSettingsService> showCodeBreakpointSettingsService) {
			this.dbgManager = dbgManager;
			this.moduleIdProvider = moduleIdProvider;
			this.dbgCodeBreakpointsService = dbgCodeBreakpointsService;
			this.dbgDotNetCodeLocationFactory = dbgDotNetCodeLocationFactory;
			this.showCodeBreakpointSettingsService = showCodeBreakpointSettingsService;
		}

		public void Add(MethodDef[] methods, bool tracepoint) {
			DbgCodeBreakpointSettings settings;
			if (tracepoint) {
				var newSettings = showCodeBreakpointSettingsService.Value.Show(new DbgCodeBreakpointSettings { IsEnabled = true, Trace = new DbgCodeBreakpointTrace(string.Empty, true) });
				if (newSettings == null)
					return;
				settings = newSettings.Value;
			}
			else
				settings = new DbgCodeBreakpointSettings { IsEnabled = true };

			var list = new List<DbgCodeBreakpointInfo>(methods.Length);
			var existing = new HashSet<DbgDotNetCodeLocation>(dbgCodeBreakpointsService.Value.Breakpoints.Select(a => a.Location).OfType<DbgDotNetCodeLocation>());
			List<DbgObject> objsToClose = null;
			foreach (var method in methods) {
				if (method.IsAbstract || method.Body == null)
					continue;
				var moduleId = moduleIdProvider.Value.Create(method.Module);
				var location = dbgDotNetCodeLocationFactory.Value.Create(moduleId, method.MDToken.Raw, 0);
				if (existing.Contains(location)) {
					if (objsToClose == null)
						objsToClose = new List<DbgObject>();
					objsToClose.Add(location);
					continue;
				}
				existing.Add(location);
				list.Add(new DbgCodeBreakpointInfo(location, settings));
			}
			if (objsToClose != null)
				dbgManager.Value.Close(objsToClose);
			dbgCodeBreakpointsService.Value.Add(list.ToArray());
		}
	}

	static class AddClassBreakpointCtxMenuCommands {
		static IMDTokenProvider GetReference(IMenuItemContext context, Guid guid) =>
			AddMethodBreakpointCtxMenuCommands.GetReference(context, guid);

		static ITypeDefOrRef GetTypeRef(IMenuItemContext context, Guid guid) =>
			GetReference(context, guid) as ITypeDefOrRef;

		abstract class MenuItemCommon : MenuItemBase {
			readonly Lazy<MethodBreakpointsService> methodBreakpointsService;
			readonly bool tracepoint;
			readonly Guid guid;

			protected MenuItemCommon(Lazy<MethodBreakpointsService> methodBreakpointsService, bool tracepoint, string guid) {
				this.methodBreakpointsService = methodBreakpointsService;
				this.tracepoint = tracepoint;
				this.guid = Guid.Parse(guid);
			}

			public override bool IsVisible(IMenuItemContext context) => GetTypeRef(context, guid) != null;
			public override bool IsEnabled(IMenuItemContext context) => GetTypeRef(context, guid) != null;

			public override void Execute(IMenuItemContext context) {
				var type = GetTypeRef(context, guid)?.ResolveTypeDef();
				if (type == null)
					return;
				methodBreakpointsService.Value.Add(type.Methods.ToArray(), tracepoint);
			}
		}

		[ExportMenuItem(Header = "res:AddClassBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_DOCVIEWER_DEBUG, Order = 100)]
		sealed class BreakpointCodeCommand : MenuItemCommon {
			[ImportingConstructor]
			BreakpointCodeCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_DOCUMENTVIEWERCONTROL_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_DOCUMENTS_DEBUG, Order = 100)]
		sealed class BreakpointAssemblyExplorerCommand : MenuItemCommon {
			[ImportingConstructor]
			BreakpointAssemblyExplorerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_DOCUMENTS_TREEVIEW_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_SEARCH_DEBUG, Order = 100)]
		sealed class BreakpointSearchCommand : MenuItemCommon {
			[ImportingConstructor]
			BreakpointSearchCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_SEARCH_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_ANALYZER_DEBUG, Order = 100)]
		sealed class BreakpointAnalyzerCommand : MenuItemCommon {
			[ImportingConstructor]
			BreakpointAnalyzerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_ANALYZER_TREEVIEW_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassTracepointCommand", Group = MenuConstants.GROUP_CTX_DOCVIEWER_DEBUG, Order = 101)]
		sealed class TracepointCodeCommand : MenuItemCommon {
			[ImportingConstructor]
			TracepointCodeCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, true, MenuConstants.GUIDOBJ_DOCUMENTVIEWERCONTROL_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassTracepointCommand", Group = MenuConstants.GROUP_CTX_DOCUMENTS_DEBUG, Order = 101)]
		sealed class TracepointAssemblyExplorerCommand : MenuItemCommon {
			[ImportingConstructor]
			TracepointAssemblyExplorerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, true, MenuConstants.GUIDOBJ_DOCUMENTS_TREEVIEW_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassTracepointCommand", Group = MenuConstants.GROUP_CTX_SEARCH_DEBUG, Order = 101)]
		sealed class TracepointSearchCommand : MenuItemCommon {
			[ImportingConstructor]
			TracepointSearchCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, true, MenuConstants.GUIDOBJ_SEARCH_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddClassTracepointCommand", Group = MenuConstants.GROUP_CTX_ANALYZER_DEBUG, Order = 101)]
		sealed class TracepointAnalyzerCommand : MenuItemCommon {
			[ImportingConstructor]
			TracepointAnalyzerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, true, MenuConstants.GUIDOBJ_ANALYZER_TREEVIEW_GUID) { }
		}
	}

	static class AddMethodBreakpointCtxMenuCommands {
		internal static IMDTokenProvider GetReference(IMenuItemContext context, Guid guid) {
			if (context.CreatorObject.Guid != guid)
				return null;

			var @ref = context.Find<TextReference>();
			if (@ref != null) {
				var realRef = @ref.Reference;
				if (realRef is Parameter)
					realRef = ((Parameter)realRef).ParamDef;
				if (realRef is IMDTokenProvider)
					return (IMDTokenProvider)realRef;
			}

			var nodes = context.Find<TreeNodeData[]>();
			if (nodes != null && nodes.Length != 0) {
				if (nodes[0] is IMDTokenNode node)
					return node.Reference;
			}

			return null;
		}

		static IMethod[] GetMethodReferences(IMenuItemContext context, Guid guid) {
			var @ref = GetReference(context, guid);

			if (@ref is IMethod methodRef)
				return methodRef.IsMethod ? new[] { methodRef } : null;

			if (@ref is PropertyDef prop) {
				var list = new List<IMethod>();
				list.AddRange(prop.GetMethods);
				list.AddRange(prop.SetMethods);
				list.AddRange(prop.OtherMethods);
				return list.Count == 0 ? null : list.ToArray();
			}

			if (@ref is EventDef evt) {
				var list = new List<IMethod>();
				if (evt.AddMethod != null)
					list.Add(evt.AddMethod);
				if (evt.RemoveMethod != null)
					list.Add(evt.RemoveMethod);
				if (evt.InvokeMethod != null)
					list.Add(evt.InvokeMethod);
				list.AddRange(evt.OtherMethods);
				return list.Count == 0 ? null : list.ToArray();
			}

			return null;
		}

		abstract class MenuItemCommon : MenuItemBase {
			readonly Lazy<MethodBreakpointsService> methodBreakpointsService;
			readonly bool tracepoint;
			readonly Guid guid;

			protected MenuItemCommon(Lazy<MethodBreakpointsService> methodBreakpointsService, bool tracepoint, string guid) {
				this.methodBreakpointsService = methodBreakpointsService;
				this.tracepoint = tracepoint;
				this.guid = Guid.Parse(guid);
			}

			public override bool IsVisible(IMenuItemContext context) => GetMethodReferences(context, guid) != null;
			public override bool IsEnabled(IMenuItemContext context) => GetMethodReferences(context, guid) != null;

			public override void Execute(IMenuItemContext context) {
				var methodRefs = GetMethodReferences(context, guid);
				if (methodRefs == null)
					return;
				methodBreakpointsService.Value.Add(methodRefs.Select(a => a.ResolveMethodDef()).Where(a => a != null).ToArray(), tracepoint);
			}
		}

		[ExportMenuItem(Header = "res:AddMethodBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_DOCVIEWER_DEBUG, Order = 150)]
		sealed class CodeCommand : MenuItemCommon {
			[ImportingConstructor]
			CodeCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_DOCUMENTVIEWERCONTROL_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddMethodBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_DOCUMENTS_DEBUG, Order = 150)]
		sealed class AssemblyExplorerCommand : MenuItemCommon {
			[ImportingConstructor]
			AssemblyExplorerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_DOCUMENTS_TREEVIEW_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddMethodBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_SEARCH_DEBUG, Order = 150)]
		sealed class SearchCommand : MenuItemCommon {
			[ImportingConstructor]
			SearchCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_SEARCH_GUID) { }
		}

		[ExportMenuItem(Header = "res:AddMethodBreakpointCommand", Icon = DsImagesAttribute.CheckDot, Group = MenuConstants.GROUP_CTX_ANALYZER_DEBUG, Order = 150)]
		sealed class AnalyzerCommand : MenuItemCommon {
			[ImportingConstructor]
			AnalyzerCommand(Lazy<MethodBreakpointsService> methodBreakpointsService) : base(methodBreakpointsService, false, MenuConstants.GUIDOBJ_ANALYZER_TREEVIEW_GUID) { }
		}
	}
}
