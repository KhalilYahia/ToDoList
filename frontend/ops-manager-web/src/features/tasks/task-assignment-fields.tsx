"use client";

import { useMemo, useState } from "react";
import { X } from "lucide-react";

import {
  Badge,
  Button,
  Field,
  Input,
  Select,
} from "@/components/ui/primitives";
import { enumCode } from "@/lib/api/enums";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";

export type AssignmentMode =
  "SingleUser" | "SelectedUsers" | "AllDepartmentMembers";

type Props = {
  departmentId: string;
  mode: AssignmentMode;
  userIds: string[];
  members: Schemas["MemberDto"][];
  onModeChange: (mode: AssignmentMode) => void;
  onUserIdsChange: (userIds: string[]) => void;
};

export function TaskAssignmentFields({
  departmentId,
  mode,
  userIds,
  members,
  onModeChange,
  onUserIdsChange,
}: Props) {
  const [search, setSearch] = useState("");
  const { identity } = useAuth();
  const currentUserRole =
    identity?.realm === "tenant"
      ? enumCode("organizationRole", identity.session.membership.role)
      : "";
  const isSupervisor = currentUserRole === "Supervisor";

  const eligibleMembers = useMemo(
    () =>
      members
        .filter((member) => {
          if (!departmentId || !member.isActive) return false;
          if (enumCode("accountStatus", member.accountStatus) !== "Active") return false;

          const role = enumCode("organizationRole", member.role);
          if (role === "Supervisor") {
            // Supervisors cannot assign tasks to other supervisors
            if (isSupervisor) return false;
            // Managers can assign tasks to any active Supervisor
            return true;
          }

          if (role === "Employee") {
            return member.departmentIds.includes(departmentId);
          }

          return false;
        })
        .sort((left, right) => left.fullName.localeCompare(right.fullName)),
    [departmentId, members, isSupervisor],
  );
  const visibleMembers = eligibleMembers.filter((member) =>
    member.fullName.toLocaleLowerCase().includes(search.toLocaleLowerCase()),
  );

  function changeMode(nextMode: AssignmentMode) {
    onModeChange(nextMode);
    onUserIdsChange([]);
  }

  function toggle(userId: string) {
    onUserIdsChange(
      userIds.includes(userId)
        ? userIds.filter((id) => id !== userId)
        : [...userIds, userId],
    );
  }

  return (
    <div className="grid gap-4 md:col-span-2">
      <Field label="Assignment" required>
        <Select
          value={mode}
          onChange={(event) => changeMode(event.target.value as AssignmentMode)}
        >
          <option value="SingleUser">One assignee (Employee / Supervisor)</option>
          <option value="SelectedUsers">Selected assignees (Employees / Supervisors)</option>
          <option value="AllDepartmentMembers">
            Everyone in the department
          </option>
        </Select>
      </Field>

      {!departmentId ? (
        <p className="text-ink-600 text-sm">
          Select a department before choosing assignees.
        </p>
      ) : mode === "SingleUser" ? (
        <Field label="Assignee" required>
          <Select
            value={userIds[0] ?? ""}
            onChange={(event) =>
              onUserIdsChange(event.target.value ? [event.target.value] : [])
            }
          >
            <option value="">Select employee or supervisor</option>
            {eligibleMembers.map((member) => {
              const role = enumCode("organizationRole", member.role);
              const roleTag = role === "Supervisor" ? " (Supervisor)" : "";
              return (
                <option key={member.userId} value={member.userId}>
                  {member.fullName}{roleTag}
                </option>
              );
            })}
          </Select>
        </Field>
      ) : mode === "SelectedUsers" ? (
        <div className="grid gap-3">
          <Field label="Search assignees">
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search active department employees and supervisors"
            />
          </Field>
          <div className="flex flex-wrap gap-2">
            {userIds.map((userId) => {
              const member = eligibleMembers.find(
                (candidate) => candidate.userId === userId,
              );
              const role = member ? enumCode("organizationRole", member.role) : "";
              const roleTag = role === "Supervisor" ? " (Supervisor)" : "";
              return (
                <Badge key={userId} tone="info">
                  {member ? `${member.fullName}${roleTag}` : userId}
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Remove ${member?.fullName ?? "assignee"}`}
                    onClick={() => toggle(userId)}
                  >
                    <X className="size-3" />
                  </Button>
                </Badge>
              );
            })}
          </div>
          <div className="border-ink-950/10 grid max-h-52 gap-1 overflow-auto rounded-xl border p-2">
            {visibleMembers.map((member) => {
              const role = enumCode("organizationRole", member.role);
              return (
                <label
                  key={member.userId}
                  className="hover:bg-ink-950/5 flex items-center gap-2 rounded-lg p-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={userIds.includes(member.userId)}
                    onChange={() => toggle(member.userId)}
                  />
                  <span>
                    {member.fullName}
                    {role === "Supervisor" ? (
                      <span className="ml-1.5 rounded bg-sky-100 px-1.5 py-0.5 text-xs font-semibold text-sky-700">
                        Supervisor
                      </span>
                    ) : null}
                  </span>
                </label>
              );
            })}
          </div>
          <p className="text-ink-600 text-sm">
            Select assignees. Each receives an independent task copy.
          </p>
        </div>
      ) : (
        <p className="border-info-200 bg-info-50 text-info-900 rounded-xl border p-4 text-sm">
          A separate task copy will be created for every active member (employee or supervisor) in this department.
        </p>
      )}
    </div>
  );
}
