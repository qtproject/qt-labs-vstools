/****************************************************************************
**
** Copyright (C) 2016 The Qt Company Ltd.
** Contact: https://www.qt.io/licensing/
**
** This file is part of the Qt VS Tools.
**
** $QT_BEGIN_LICENSE:GPL-EXCEPT$
** Commercial License Usage
** Licensees holding valid commercial Qt licenses may use this file in
** accordance with the commercial license agreement provided with the
** Software or, alternatively, in accordance with the terms contained in
** a written agreement between you and The Qt Company. For licensing terms
** and conditions see https://www.qt.io/terms-conditions. For further
** information use the contact form at https://www.qt.io/contact-us.
**
** GNU General Public License Usage
** Alternatively, this file may be used under the terms of the GNU
** General Public License version 3 as published by the Free Software
** Foundation with exceptions as appearing in the file LICENSE.GPL3-EXCEPT
** included in the packaging of this file. Please review the following
** information to ensure the GNU General Public License requirements will
** be met: https://www.gnu.org/licenses/gpl-3.0.html.
**
** $QT_END_LICENSE$
**
****************************************************************************/

#include "qmakedataprovider.h"
#include "evalhandler.h"
#include <qmakeevaluator.h>
#include <qmakeglobals.h>
#include <QtCore/QFileInfo>
#include <QtCore/QList>
#include <QtCore/QPair>

class QMakeDataProviderPrivate
{
public:
    QStringList m_headerFiles;
    QStringList m_sourceFiles;
    QStringList m_resourceFiles;
    QStringList m_formFiles;
    typedef QPair<QStringList *, ProKey> Mapping;
    QList<Mapping> m_variableMappings;
    bool m_valid;
    bool m_flat;
    QString m_qtdir;

    QMakeDataProviderPrivate()
    {
        m_variableMappings
                << qMakePair(&m_headerFiles, ProKey("HEADERS"))
                << qMakePair(&m_sourceFiles, ProKey("SOURCES"))
                << qMakePair(&m_resourceFiles, ProKey("RESOURCES"))
                << qMakePair(&m_formFiles, ProKey("FORMS"));
    }

    bool readFile(const QString &fileName)
    {
        QFileInfo fi(fileName);
        if (fi.isRelative())
            qWarning("qmakewrapper: expecting an absolute filename.");

        m_headerFiles.clear();
        m_sourceFiles.clear();
        m_resourceFiles.clear();
        m_formFiles.clear();
        m_valid = false;
        m_flat = true;

        QMakeGlobals globals;
        ProFileCache proFileCache;
        EvalHandler handler;
        QMakeParser parser(&proFileCache, &handler);
        QMakeEvaluator evaluator(&globals, &parser, &handler);
        if (evaluator.evaluateFile(fileName, QMakeHandler::EvalProjectFile,
                                   QMakeEvaluator::LoadProOnly) != QMakeEvaluator::ReturnTrue)
        {
            qWarning("qmakewrapper: failed to parse %s", qPrintable(fileName));
            return false;
        }

        m_valid = true;
        m_flat = evaluator.isActiveConfig(QStringLiteral("flat"));

        foreach (const Mapping &mapping, m_variableMappings)
            *mapping.first = evaluator.values(mapping.second).toQStringList();

        return true;
    }
};

QMakeDataProvider::QMakeDataProvider()
    : d(new QMakeDataProviderPrivate())
{
}

QMakeDataProvider::~QMakeDataProvider()
{
    delete d;
}

bool QMakeDataProvider::readFile(const QString &fileName)
{
    return d->readFile(fileName);
}

void QMakeDataProvider::setQtDir(const QString &qtdir)
{
    d->m_qtdir = qtdir;
}

QStringList QMakeDataProvider::getFormFiles() const
{
    return d->m_formFiles;
}

QStringList QMakeDataProvider::getHeaderFiles() const
{
    return d->m_headerFiles;
}

QStringList QMakeDataProvider::getResourceFiles() const
{
    return d->m_resourceFiles;
}

QStringList QMakeDataProvider::getSourceFiles() const
{
    return d->m_sourceFiles;
}

bool QMakeDataProvider::isFlat() const
{
    return d->m_flat;
}

bool QMakeDataProvider::isValid() const
{
    return d->m_valid;
}
