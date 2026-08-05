import { enumCode, enumValue, statusTone } from "./enums";

describe("wire enum mapping", () => {
  it("maps stable numeric API values to UI codes and back", () => {
    expect(enumCode("taskStatus", 3)).toBe("PendingApproval");
    expect(enumValue("orderStatus", "Received")).toBe(6);
    expect(enumCode("subscriptionAccess", 2)).toBe("ReadOnly");
    expect(enumCode("recurrence", 2)).toBe("Monthly");
    expect(enumValue("recurrence", "Custom")).toBe(-1);
    expect(enumCode("taskAssignmentMode", 2)).toBe("AllDepartmentMembers");
  });

  it("assigns meaningful status tones", () => {
    expect(statusTone("Completed")).toBe("success");
    expect(statusTone("Expired")).toBe("danger");
    expect(statusTone("GraceLimited")).toBe("warning");
  });
});
