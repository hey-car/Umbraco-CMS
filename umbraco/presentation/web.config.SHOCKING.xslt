﻿<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl">
  <xsl:output method="xml" indent="yes"/>

  <!-- Set up a local connection string -->
  <xsl:template match="/configuration/appSettings/add[@key='umbracoDbDSN']/@value">
    <xsl:attribute name="value">server=SHOCKING\SQLEXPRESS;database=UmbracoTest5;user id=sa;password=test</xsl:attribute>
  </xsl:template>
  
  <xsl:template match="/configuration/appSettings/add[@key='umbracoConfigurationStatus']/@value">
    <xsl:attribute name="value">4.1.0.betaII</xsl:attribute>
  </xsl:template>
  
  <xsl:template match="/configuration/appSettings/add[@key='umbracoContentXML']/@value">
    <xsl:attribute name="value">~/App_Data/umbraco.config</xsl:attribute>
  </xsl:template>

  <xsl:template match="/configuration/appSettings/add[@key='umbracoStorageDirectory']/@value">
    <xsl:attribute name="value">~/App_Data</xsl:attribute>
  </xsl:template>

  <xsl:template match="/configuration/appSettings/add[@key='umbracoPath']/@value">
    <xsl:attribute name="value">~/umbraco</xsl:attribute>
  </xsl:template>

  <!-- Add trace output -->
  <!--<xsl:template match="/configuration">
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:apply-templates/>
      <system.diagnostics>
        <trace autoflush="true">
          <listeners>
            <add name="SqlListener" type="System.Diagnostics.TextWriterTraceListener" initializeData="trace.log" />
          </listeners>
        </trace>
      </system.diagnostics>
    </xsl:copy>
  </xsl:template>-->
  
  <!-- Default templates to match anything else -->
  <xsl:template match="@*">
    <xsl:copy/>
  </xsl:template>

  <xsl:template match="node()">
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:apply-templates/>
    </xsl:copy>
  </xsl:template> 
</xsl:stylesheet>
