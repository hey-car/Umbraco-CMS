﻿<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ModuleSelector.ascx.cs" Inherits="umbraco.presentation.umbraco.LiveEditing.Modules.SkinModule.ModuleSelector" %>
<%@ Import Namespace="umbraco.cms.businesslogic.packager.repositories"  %>
<div id="moduleSelectorContainer">

<asp:Repeater ID="rep_modules" runat="server" 
    onitemdatabound="rep_modules_ItemDataBound">
    <HeaderTemplate>
        <ul id="modules">
    </HeaderTemplate>
    <ItemTemplate>
        <li>

        <asp:HyperLink ID="ModuleSelectLink" runat="server" NavigateUrl="javascript:void(0);">
            <img width="25px" src="http://our.umbraco.org/<%# ((Package)Container.DataItem).Thumbnail %>" alt="<%# ((Package)Container.DataItem).Text %>" />
            <span><%# ((Package)Container.DataItem).Text %></span>
        
        </asp:HyperLink>

        </li>
    </ItemTemplate>
    <FooterTemplate>
        </ul>
    </FooterTemplate>

   
</asp:Repeater>

 <p id="installingModule" style="display:none;">Installing module...</p>

 <p id="moduleSelect" style="display:none;">Select where to place the module</p>

 <a href="javascript:void(0);" onclick="jQuery('.ModuleSelector').hide();umbRemoveModuleContainerSelectors();">Cancel</a>
 </div>
