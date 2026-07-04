<?xml version="1.0" encoding="ISO-8859-1"?>
<xsl:stylesheet version="1.0"
xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:template match="/">
    <html>
      <body>
        <h2>Requirement Table</h2>
        <table border="8">
          <tr bgcolor="#58acfa">
            <th>All Covered Requirements</th>
          </tr>
          <xsl:for-each select="RequirementTable/Requirement">
            <tr>
              <td>
                <xsl:value-of select="."/>
              </td>
            </tr>
          </xsl:for-each>
        </table>
        <p></p>
        <table border="8">
          <tr bgcolor="#58acfa">
            <th>Action Name</th>
            <th>Requirements covered by action</th>
          </tr>
          <xsl:for-each select="RequirementTable/ActionCoveredRequirement">
            <tr>
              <td>
                <xsl:value-of select="Action"/>
              </td>
              <td>
                <ul>
                  <xsl:for-each select="Requirement">
                    <li>
                        <xsl:value-of select="."/>
                    </li>
                  </xsl:for-each>
                </ul>
              </td>
            </tr>
          </xsl:for-each>
        </table>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
