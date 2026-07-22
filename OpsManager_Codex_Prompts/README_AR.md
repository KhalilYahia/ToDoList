# حزمة برومبتات Codex لمشروع OpsManager

تتضمن الحزمة أربعة ملفات رئيسية:

1. `.agents/skills/ops-manager-project/SKILL.md`  
   تعريف المشروع، النطاق، التقنيات، قواعد N-tier وDDD، قواعد Repository وUnitOfWork، الأمان، الاختبارات والتوثيق.

2. `prompts/01-backend-structure-and-entities.md`  
   ينشئ Solution الخلفية، المشاريع والاعتماديات، Domain Entities، EF Core configurations، PostgreSQL migration، GenericRepository وUnitOfWork، الاختبارات والتوثيق.

3. `prompts/02-backend-logic-and-apis.md`  
   ينفذ المصادقة والصلاحيات، Services، APIs، المهام والجدولة، طلبات الأقسام، الشكاوى، الاشتراك اليدوي، الإشعارات والتقارير.

4. `prompts/03-frontend-project.md`  
   ينشئ مشروع Next.js ويطبق الواجهات الأساسية، الصلاحيات، التقويم، الطلبات، الشكاوى، التقارير، الاشتراك، ولوحة إدارة المنصة.

يوجد أيضًا `AGENTS.md` صغير في جذر الحزمة ليطلب من Codex تطبيق الـSkill على جميع أعمال المستودع.

## طريقة الاستخدام

1. فك ضغط الحزمة وانسخ محتوياتها إلى جذر مستودع جديد.
2. افتح Codex من جذر المستودع.
3. ابدأ بتنفيذ:
   `prompts/01-backend-structure-and-entities.md`
4. راجع نتيجة كل Batch والبناء والاختبارات قبل الانتقال إلى الملف التالي.
5. بعد اكتمال البنية الخلفية نفّذ:
   `prompts/02-backend-logic-and-apis.md`
6. بعد اكتمال الـAPI نفّذ:
   `prompts/03-frontend-project.md`

يمكن ذكر الـSkill صراحة في طلب Codex باستخدام:

```text
$ops-manager-project
```

## قرارات النطاق المثبتة

- ASP.NET Core Web API + EF Core + PostgreSQL.
- N-tier مع DDD-inspired domain model.
- Entities وcontracts في Domain.
- GenericRepository وUnitOfWork في Repository.
- Business logic وDTOs في Service.
- Controllers رقيقة في API.
- لا استعلام مباشر لـDbContext من API أو Service.
- Next.js + React + TypeScript للواجهة.
- دعم العربية والإنجليزية والروسية لواجهة النظام فقط.
- محتوى المهام والطلبات والأقسام يبقى باللغة التي كُتب بها.
- لا ورديات، لا تصنيفات، لا وسوم، ولا مخزون.
- الاشتراك والدفع يدويان في MVP.
- التقارير الأساسية ضمن النطاق.
