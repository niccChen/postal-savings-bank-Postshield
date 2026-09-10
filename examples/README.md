# Sample workbooks

These workbooks contain generated demonstration records, not customer data. Each has one sheet and two records. Mobile numbers are stored as text so they follow the application's string-cell processing path.

Copy the XLSX files into a separate working directory before running the demo. The application saves masking changes into the files it processes.

1. Choose the working directory and click **Scan .xlsx**. Two files should appear.
2. Search for `contacts-a` to show one file, then click **Clear** to restore the two-file list.
3. Click **Apply Masking**. The status should report two successfully processed files.
4. Inspect the workbooks: names retain their first character, and mobile numbers retain their first three and last four digits. Record IDs and regions stay unchanged.
5. Click **Undo Masking** before starting another batch or changing directories. Both workbooks should return to their original contents.

| File | Name example | Mobile example |
| --- | --- | --- |
| `demo-contacts-a.xlsx` | `张示例` → `张**` | `13800001234` → `138****1234` |
| `demo-contacts-b.xlsx` | `王测试` → `王**` | `13700009012` → `137****9012` |

These examples cover names and mobile numbers. They do not validate every rule in the default XML configuration.
